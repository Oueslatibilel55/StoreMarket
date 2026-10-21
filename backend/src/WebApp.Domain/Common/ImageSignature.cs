namespace WebApp.Domain.Common;

/// <summary>
/// Recognises an image from its first bytes ("magic numbers"), never from the file name or the
/// Content-Type the browser sent — both are chosen by the uploader. Only raster formats a browser
/// shows safely are accepted: no SVG (it can carry scripts).
/// </summary>
public static class ImageSignature
{
    /// <summary>Bytes needed to recognise every accepted format.</summary>
    public const int HeaderLength = 12;

    public sealed record Format(string ContentType, string Extension);

    public static readonly Format Jpeg = new("image/jpeg", ".jpg");
    public static readonly Format Png = new("image/png", ".png");
    public static readonly Format Webp = new("image/webp", ".webp");

    public static Format? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return Jpeg;
        if (header.Length >= 8 && header[..8].SequenceEqual(stackalloc byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return Png;
        // RIFF....WEBP
        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8)) return Webp;
        return null;
    }
}
