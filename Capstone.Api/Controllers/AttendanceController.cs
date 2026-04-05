using System.Globalization;
using Capstone.Api.Data;
using Capstone.Api.Models;
using Capstone.Api.Security;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("management/attendance")]
[Authorize]
public sealed class AttendanceController : ControllerBase
{
    private readonly SqlConnectionFactory _db;
    public AttendanceController(SqlConnectionFactory db) => _db = db;

    // ---------- DTOs ----------
    public sealed class PunchRequest
    {
        public string EventType { get; set; } = "";   // ShiftStart, ShiftEnd, BreakStart, BreakEnd
        public string? BreakType { get; set; }        // Lunch, ShortBreak
        public DateTime? OccurredAtUtc { get; set; }  // optional (admin correction / offline)
        public string Source { get; set; } = "App";   // App/Web/Admin
        public string? Note { get; set; }
    }

    public sealed class VoidRequest
    {
        public string Reason { get; set; } = "";
    }

    public sealed class TimeBlockDto
    {
        public string Type { get; set; } = "";     // Shift or Break
        public string? BreakType { get; set; }     // Lunch/ShortBreak
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public int? StartEventId { get; set; }
        public int? EndEventId { get; set; }
    }

    public sealed class DailySummaryDto
    {
        public DateOnly Day { get; set; }
        public int MinutesWorked { get; set; }
        public int MinutesBreaks { get; set; }
        public int MinutesShift { get; set; }
        public int ShortBreakCount { get; set; }
        public bool HasOpenShift { get; set; }
        public bool HasOpenBreak { get; set; }
    }

    // ---------- Helpers ----------
    private static readonly HashSet<string> ValidEventTypes = new(StringComparer.OrdinalIgnoreCase)
        { "ShiftStart", "ShiftEnd", "BreakStart", "BreakEnd" };

    private static readonly HashSet<string> ValidBreakTypes = new(StringComparer.OrdinalIgnoreCase)
        { "Lunch", "ShortBreak" };

    private static void ValidatePunch(PunchRequest r)
    {
        if (!ValidEventTypes.Contains(r.EventType))
            throw new ArgumentException("Invalid punch type. Please try again.");

        if ((r.EventType.Equals("BreakStart", StringComparison.OrdinalIgnoreCase)
          || r.EventType.Equals("BreakEnd", StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrWhiteSpace(r.BreakType) || !ValidBreakTypes.Contains(r.BreakType))
                throw new ArgumentException("Please select a break type (Lunch or Short Break).");
        }
        else
        {
            r.BreakType = null;
        }
    }

    // ---------- 2.1 Punch (self) ----------
    [HttpPost("punch")]
    public async Task<ActionResult> Punch([FromBody] PunchRequest req)
    {
        if (!Perm.Has(User, "ATTENDANCE_PUNCH")) return Forbid();

        ValidatePunch(req);

        var userId = Perm.UserId(User);
        var nowUtc = DateTime.UtcNow;
        var whenUtc = req.OccurredAtUtc ?? nowUtc;

        await using var conn = _db.Create();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            // Basic state validation (prevents garbage data)
            var openShift = await conn.QuerySingleAsync<int>(@"
SELECT COUNT(1)
FROM dbo.AttendanceEvents WITH (UPDLOCK, HOLDLOCK)
WHERE UserId=@UserId AND IsVoided=0 AND EventType='ShiftStart'
  AND NOT EXISTS (
     SELECT 1 FROM dbo.AttendanceEvents e2
     WHERE e2.UserId=@UserId AND e2.IsVoided=0 AND e2.EventType='ShiftEnd'
       AND e2.OccurredAt > dbo.AttendanceEvents.OccurredAt
  );", new { UserId = userId }, tx);

            var openBreak = await conn.QuerySingleAsync<int>(@"
SELECT COUNT(1)
FROM dbo.AttendanceEvents WITH (UPDLOCK, HOLDLOCK)
WHERE UserId=@UserId AND IsVoided=0 AND EventType='BreakStart'
  AND NOT EXISTS (
     SELECT 1 FROM dbo.AttendanceEvents e2
     WHERE e2.UserId=@UserId AND e2.IsVoided=0 AND e2.EventType='BreakEnd'
       AND e2.OccurredAt > dbo.AttendanceEvents.OccurredAt
  );", new { UserId = userId }, tx);

            // Rules
            if (req.EventType.Equals("ShiftStart", StringComparison.OrdinalIgnoreCase) && openShift > 0)
            { tx.Rollback(); return BadRequest(new ApiError("Shift already started.")); }

            if (req.EventType.Equals("ShiftEnd", StringComparison.OrdinalIgnoreCase) && openShift == 0)
            { tx.Rollback(); return BadRequest(new ApiError("No open shift to end.")); }

            if (req.EventType.StartsWith("Break", StringComparison.OrdinalIgnoreCase))
            {
                if (openShift == 0) { tx.Rollback(); return BadRequest(new ApiError("Start a shift first.")); }

                if (req.EventType.Equals("BreakStart", StringComparison.OrdinalIgnoreCase) && openBreak > 0)
                { tx.Rollback(); return BadRequest(new ApiError("Break already started.")); }

                if (req.EventType.Equals("BreakEnd", StringComparison.OrdinalIgnoreCase) && openBreak == 0)
                { tx.Rollback(); return BadRequest(new ApiError("No open break to end.")); }
            }

            // Break limits
            if (req.EventType.Equals("BreakStart", StringComparison.OrdinalIgnoreCase)
                && req.BreakType!.Equals("ShortBreak", StringComparison.OrdinalIgnoreCase))
            {
                var todayUtcStart = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc);
                var todayUtcEnd = todayUtcStart.AddDays(1);

                var shortBreakStarts = await conn.QuerySingleAsync<int>(@"
SELECT COUNT(1)
FROM dbo.AttendanceEvents
WHERE UserId=@UserId AND IsVoided=0
  AND EventType='BreakStart' AND BreakType='ShortBreak'
  AND OccurredAt >= @Start AND OccurredAt < @End;", new { UserId = userId, Start = todayUtcStart, End = todayUtcEnd }, tx);

                if (shortBreakStarts >= 2)
                { tx.Rollback(); return BadRequest(new ApiError("Short break limit reached for today (2).")); }
            }

            // Insert punch
            var eventId = await conn.QuerySingleAsync<int>(@"
INSERT INTO dbo.AttendanceEvents (UserId, EventType, BreakType, OccurredAt, Source, Note)
OUTPUT INSERTED.EventId
VALUES (@UserId, @EventType, @BreakType, @OccurredAt, @Source, @Note);",
                new
                {
                    UserId = userId,
                    EventType = req.EventType,
                    BreakType = req.BreakType,
                    OccurredAt = whenUtc,
                    Source = string.IsNullOrWhiteSpace(req.Source) ? "App" : req.Source,
                    Note = req.Note
                }, tx);

            // Audit
            await conn.ExecuteAsync(@"
INSERT INTO dbo.AttendanceAudit(Action, TargetEventId, TargetUserId, ActorUserId, Detail)
VALUES ('PUNCH', @EventId, @TargetUserId, @ActorUserId, @Detail);",
                new
                {
                    EventId = eventId,
                    TargetUserId = userId,
                    ActorUserId = userId,
                    Detail = $"{req.EventType} {req.BreakType}".Trim()
                }, tx);

            tx.Commit();
            return Ok(new { eventId });
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // ---------- 2.2 View calendar blocks (me) ----------
    [HttpGet("me/blocks")]
    public async Task<ActionResult<List<TimeBlockDto>>> MyBlocks([FromQuery] string from, [FromQuery] string to)
    {
        var uid = Perm.UserId(User);
        return await BlocksForUserInternal(uid, from, to, allowAnyUser: false);
    }

    // ---------- 2.3 View calendar blocks (manager/TL) ----------
    [HttpGet("users/{userId:int}/blocks")]
    public async Task<ActionResult<List<TimeBlockDto>>> UserBlocks(int userId, [FromQuery] string from, [FromQuery] string to)
    {
        // self OR attendance admin
        var uid = Perm.UserId(User);
        var isSelf = uid == userId;
        if (!isSelf && !Perm.Has(User, "ATTENDANCE_VIEW_ALL")) return Forbid();

        return await BlocksForUserInternal(userId, from, to, allowAnyUser: true);
    }

    private async Task<ActionResult<List<TimeBlockDto>>> BlocksForUserInternal(int userId, string from, string to, bool allowAnyUser)
    {
        if (!DateTime.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var fromUtc))
            return BadRequest(new ApiError("Please enter a valid start date."));
        if (!DateTime.TryParse(to, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var toUtc))
            return BadRequest(new ApiError("Please enter a valid end date."));

        // widen slightly so block pairing works
        var qFrom = fromUtc.AddDays(-1);
        var qTo = toUtc.AddDays(1);

        await using var conn = _db.Create();
        var events = (await conn.QueryAsync<dynamic>(@"
SELECT EventId, EventType, BreakType, OccurredAt
FROM dbo.AttendanceEvents
WHERE UserId=@UserId AND IsVoided=0 AND OccurredAt >= @From AND OccurredAt < @To
ORDER BY OccurredAt ASC;", new { UserId = userId, From = qFrom, To = qTo }))
            .Select(e => new
            {
                EventId = (int)e.EventId,
                EventType = (string)e.EventType,
                BreakType = (string?)e.BreakType,
                OccurredAt = (DateTime)e.OccurredAt
            })
            .ToList();

        // Pair ShiftStart/ShiftEnd and BreakStart/BreakEnd in order
        var blocks = new List<TimeBlockDto>();
        (int id, DateTime t)? openShift = null;
        (int id, DateTime t, string breakType)? openBreak = null;

        foreach (var ev in events)
        {
            if (ev.EventType.Equals("ShiftStart", StringComparison.OrdinalIgnoreCase))
                openShift ??= (ev.EventId, ev.OccurredAt);

            else if (ev.EventType.Equals("ShiftEnd", StringComparison.OrdinalIgnoreCase) && openShift is not null)
            {
                blocks.Add(new TimeBlockDto
                {
                    Type = "Shift",
                    StartUtc = openShift.Value.t,
                    EndUtc = ev.OccurredAt,
                    StartEventId = openShift.Value.id,
                    EndEventId = ev.EventId
                });
                openShift = null;
                openBreak = null; // safety
            }
            else if (ev.EventType.Equals("BreakStart", StringComparison.OrdinalIgnoreCase))
            {
                if (openBreak is null)
                    openBreak = (ev.EventId, ev.OccurredAt, ev.BreakType ?? "Break");
            }
            else if (ev.EventType.Equals("BreakEnd", StringComparison.OrdinalIgnoreCase) && openBreak is not null)
            {
                blocks.Add(new TimeBlockDto
                {
                    Type = "Break",
                    BreakType = openBreak.Value.breakType,
                    StartUtc = openBreak.Value.t,
                    EndUtc = ev.OccurredAt,
                    StartEventId = openBreak.Value.id,
                    EndEventId = ev.EventId
                });
                openBreak = null;
            }
        }

        // Filter back to requested window
        var filtered = blocks
            .Where(b => b.EndUtc > fromUtc && b.StartUtc < toUtc)
            .ToList();

        return Ok(filtered);
    }

    // ---------- 2.4 Daily summary (minutes worked, breaks, counts) ----------
    [HttpGet("users/{userId:int}/summary")]
    public async Task<ActionResult<List<DailySummaryDto>>> UserSummary(int userId, [FromQuery] string from, [FromQuery] string to)
    {
        var uid = Perm.UserId(User);
        var isSelf = uid == userId;
        if (!isSelf && !Perm.Has(User, "ATTENDANCE_VIEW_ALL")) return Forbid();

        if (!DateTime.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var fromUtc))
            return BadRequest(new ApiError("Please enter a valid start date."));
        if (!DateTime.TryParse(to, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var toUtc))
            return BadRequest(new ApiError("Please enter a valid end date."));

        // We compute summary from blocks (simple + consistent)
        var blocksRes = await BlocksForUserInternal(userId, from, to, allowAnyUser: true);
        if (blocksRes.Result is ObjectResult bad && bad.StatusCode >= 400) return bad;

        var blocks = (blocksRes.Value ?? new()).ToList();

        var daySummaries = new Dictionary<DateOnly, DailySummaryDto>();

        foreach (var shift in blocks.Where(b => b.Type == "Shift"))
        {
            var day = DateOnly.FromDateTime(shift.StartUtc.Date);
            if (!daySummaries.TryGetValue(day, out var s))
                daySummaries[day] = s = new DailySummaryDto { Day = day };

            var shiftMins = (int)Math.Max(0, (shift.EndUtc - shift.StartUtc).TotalMinutes);
            s.MinutesShift += shiftMins;
        }

        foreach (var brk in blocks.Where(b => b.Type == "Break"))
        {
            var day = DateOnly.FromDateTime(brk.StartUtc.Date);
            if (!daySummaries.TryGetValue(day, out var s))
                daySummaries[day] = s = new DailySummaryDto { Day = day };

            var breakMins = (int)Math.Max(0, (brk.EndUtc - brk.StartUtc).TotalMinutes);
            s.MinutesBreaks += breakMins;

            if (string.Equals(brk.BreakType, "ShortBreak", StringComparison.OrdinalIgnoreCase))
                s.ShortBreakCount += 1;
        }

        foreach (var kv in daySummaries)
        {
            kv.Value.MinutesWorked = Math.Max(0, kv.Value.MinutesShift - kv.Value.MinutesBreaks);
        }

        return Ok(daySummaries.Values.OrderBy(x => x.Day).ToList());
    }

    // ---------- 2.5 Manager correction: void an event ----------
    [HttpPost("events/{eventId:int}/void")]
    public async Task<ActionResult> VoidEvent(int eventId, [FromBody] VoidRequest req)
    {
        if (!Perm.Has(User, "ATTENDANCE_EDIT")) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Reason)) return BadRequest(new ApiError("Please provide a reason."));

        var actorId = Perm.UserId(User);

        await using var conn = _db.Create();

        // find target
        var target = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
SELECT TOP 1 EventId, UserId, IsVoided
FROM dbo.AttendanceEvents
WHERE EventId=@EventId;", new { EventId = eventId });

        if (target is null) return NotFound(new ApiError("This record could not be found."));
        if ((bool)target.IsVoided) return BadRequest(new ApiError("This record has already been removed."));

        await conn.ExecuteAsync(@"
UPDATE dbo.AttendanceEvents
SET IsVoided=1, VoidedAt=SYSUTCDATETIME(), VoidedByUserId=@ActorId, VoidReason=@Reason
WHERE EventId=@EventId;", new { ActorId = actorId, Reason = req.Reason, EventId = eventId });

        await conn.ExecuteAsync(@"
INSERT INTO dbo.AttendanceAudit(Action, TargetEventId, TargetUserId, ActorUserId, Detail)
VALUES ('VOID', @EventId, @TargetUserId, @ActorId, @Detail);",
            new { EventId = eventId, TargetUserId = (int)target.UserId, ActorId = actorId, Detail = req.Reason });

        return Ok();
    }

    // ---------- 2.6 Manager correction: add event for a user (admin punch) ----------
    [HttpPost("users/{userId:int}/admin-punch")]
    public async Task<ActionResult> AdminPunch(int userId, [FromBody] PunchRequest req)
    {
        if (!Perm.Has(User, "ATTENDANCE_EDIT")) return Forbid();

        ValidatePunch(req);

        var actorId = Perm.UserId(User);
        var whenUtc = req.OccurredAtUtc ?? DateTime.UtcNow;

        await using var conn = _db.Create();

        var eventId = await conn.QuerySingleAsync<int>(@"
INSERT INTO dbo.AttendanceEvents (UserId, EventType, BreakType, OccurredAt, Source, Note)
OUTPUT INSERTED.EventId
VALUES (@UserId, @EventType, @BreakType, @OccurredAt, 'Admin', @Note);",
            new { UserId = userId, EventType = req.EventType, BreakType = req.BreakType, OccurredAt = whenUtc, Note = req.Note });

        await conn.ExecuteAsync(@"
INSERT INTO dbo.AttendanceAudit(Action, TargetEventId, TargetUserId, ActorUserId, Detail)
VALUES ('ADMIN_PUNCH', @EventId, @TargetUserId, @ActorId, @Detail);",
            new { EventId = eventId, TargetUserId = userId, ActorId = actorId, Detail = $"{req.EventType} {req.BreakType} @ {whenUtc:o}" });

        return Ok(new { eventId });
    }
}
