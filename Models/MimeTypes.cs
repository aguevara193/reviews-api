using System;
using System.Collections.Generic;

public static class MimeTypes
{
    private static readonly Dictionary<string, string> MimeTypeMappings = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
    {
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png", "image/png" },
        { ".gif", "image/gif" },
        { ".bmp", "image/bmp" },
        { ".webp", "image/webp" },
        { ".avif", "image/avif" },
        // Add more mappings as needed
    };

    public static string GetMimeType(string extension)
    {
        if (extension.StartsWith("."))
        {
            extension = extension.Substring(1);
        }

        if (MimeTypeMappings.TryGetValue("." + extension, out var mimeType))
        {
            return mimeType;
        }

        return "application/octet-stream"; // Default MIME type if not found
    }
}
