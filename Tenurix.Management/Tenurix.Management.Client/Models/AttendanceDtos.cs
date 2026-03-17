namespace Tenurix.Management.Client.Models;

public sealed class AttendancePunchRequest
{
    public string EventType { get; set; } = "";
    public string? BreakType { get; set; }
    public DateTime? OccurredAtUtc { get; set; }
    public string Source { get; set; } = "App";
    public string? Note { get; set; }
}

public sealed class AttendanceTimeBlockDto
{
    public string Type { get; set; } = "";
    public string? BreakType { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public int? StartEventId { get; set; }
    public int? EndEventId { get; set; }
}

public sealed class AttendanceDailySummaryDto
{
    public string Day { get; set; } = "";
    public int MinutesWorked { get; set; }
    public int MinutesBreaks { get; set; }
    public int MinutesShift { get; set; }
    public int ShortBreakCount { get; set; }
}
