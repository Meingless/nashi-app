namespace Nashi.Core;

/// Resolves explicitly supported, distribution-managed Linux tools. Relative PATH
/// entries are skipped so opening a document cannot select a sibling executable.
public static class SystemTools
{
    public static string? Find(string name)
    {
        if(name is not ("ffmpeg" or "ffprobe" or "yt-dlp" or "deno" or "libreoffice"))
            throw new ArgumentException("Unknown system tool.");
        foreach(var directory in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator))
        {
            if(!Path.IsPathFullyQualified(directory))continue;
            var path=Path.Combine(directory,name);
            if(!File.Exists(path))continue;
            if(OperatingSystem.IsLinux())
            {
                // File.Exists accepts dangling symlinks; the target may also disappear
                // or become inaccessible before its mode is read. Try the next entry.
                UnixFileMode mode;
                try{mode=File.GetUnixFileMode(path);}
                catch(IOException){continue;}
                catch(UnauthorizedAccessException){continue;}
                if((mode&(UnixFileMode.UserExecute|UnixFileMode.GroupExecute|UnixFileMode.OtherExecute))==0)continue;
            }
            return Path.GetFullPath(path);
        }
        return null;
    }
}
