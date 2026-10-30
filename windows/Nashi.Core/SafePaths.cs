namespace Nashi.Core;

public static class SafePaths
{
    private static readonly HashSet<string> Devices = new(StringComparer.OrdinalIgnoreCase)
    { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

    public static string ArchiveEntry(string root, string entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entry);
        var name = entry.Replace('\\', '/');
        var parts = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (name.StartsWith('/') || parts.Length == 0 || parts.Any(p => p is "." or ".." ||
            p.EndsWith('.') || p.EndsWith(' ') || p.IndexOfAny(['<', '>', ':', '"', '|', '?', '*', '\0']) >= 0 ||
            Devices.Contains(p.Split('.')[0])))
            throw new InvalidDataException("The archive contains an unsafe path.");
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var destination = Path.GetFullPath(Path.Combine(fullRoot, Path.Combine(parts)));
        if (!destination.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The archive entry leaves its destination.");
        return destination;
    }

    /// Sanitizes a derived filename stem, not a path. Keep Unique's path rejection
    /// intact for callers accepting an explicit output filename.
    public static string SafeStem(string stem)
    {
        ArgumentNullException.ThrowIfNull(stem);
        var safe = string.Concat(stem.Select(c => c is ':' or '\\' or '/' or '\0' ? '_' : c));
        return safe.Length == 0 ? "Export" : safe;
    }

    public static string Unique(string directory, string filename)
    {
        if (Path.GetFileName(filename) != filename || filename.IndexOfAny(['/', '\\', ':', '\0']) >= 0)
            throw new ArgumentException("Choose a plain output filename.");
        var stem = Path.GetFileNameWithoutExtension(filename); var extension = Path.GetExtension(filename);
        for (var number = 1; number < 100_000; number++)
        {
            var candidate = Path.Combine(directory, number == 1 ? filename : $"{stem} {number}{extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
        throw new IOException("Too many files already use this name.");
    }
}
