namespace Capstone.Api.Security;

/// <summary>
/// Validates uploaded file content using magic bytes (file signatures)
/// rather than trusting the user-supplied Content-Type or file extension.
/// </summary>
public static class FileValidator
{
    // Known magic byte signatures
    private static readonly byte[] Jpeg    = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Png     = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Gif87a  = [0x47, 0x49, 0x46, 0x38, 0x37, 0x61];
    private static readonly byte[] Gif89a  = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61];
    private static readonly byte[] Pdf     = [0x25, 0x50, 0x44, 0x46]; // %PDF
    private static readonly byte[] Bmp     = [0x42, 0x4D];
    private static readonly byte[] RiffTag = [0x52, 0x49, 0x46, 0x46]; // WebP starts RIFF....WEBP
    private static readonly byte[] WebpTag = [0x57, 0x45, 0x42, 0x50];

    /// <summary>
    /// Reads the first bytes of the stream and checks they match the
    /// signature for the expected file extension. The stream position is
    /// reset to 0 after reading so callers can still copy the full file.
    /// </summary>
    public static async Task<bool> IsValidContentAsync(Stream stream, string extension)
    {
        const int bufferSize = 12;
        var header = new byte[bufferSize];
        var read = await stream.ReadAsync(header.AsMemory(0, bufferSize));
        stream.Position = 0; // reset for caller

        if (read < 2) return false;

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => StartsWith(header, Jpeg),
            ".png"            => StartsWith(header, Png),
            ".gif"            => StartsWith(header, Gif87a) || StartsWith(header, Gif89a),
            ".pdf"            => StartsWith(header, Pdf),
            ".bmp"            => StartsWith(header, Bmp),
            ".webp"           => StartsWith(header, RiffTag) && read >= 12 && StartsWith(header.AsSpan(8), WebpTag),
            _                 => false
        };
    }

    private static bool StartsWith(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        if (data.Length < signature.Length) return false;
        return data[..signature.Length].SequenceEqual(signature);
    }
}
