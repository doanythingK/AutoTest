using System.IO;
using System.Reflection;

namespace AutoTest.ErpAutomation.Services;

public static class AppBuildInfoService
{
    public static string GetAppVersion()
    {
        var assembly = typeof(AppBuildInfoService).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "(알 수 없음)";
    }

    public static string GetSourceRevision()
    {
        var environmentRevision = Environment.GetEnvironmentVariable("GIT_COMMIT")
            ?? Environment.GetEnvironmentVariable("SOURCE_VERSION");

        if (!string.IsNullOrWhiteSpace(environmentRevision))
        {
            return ShortenRevision(environmentRevision);
        }

        return TryReadGitRevision(AppContext.BaseDirectory)
            ?? TryReadGitRevision(Directory.GetCurrentDirectory())
            ?? "(알 수 없음)";
    }

    private static string? TryReadGitRevision(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            var gitPath = Path.Combine(directory.FullName, ".git");
            var gitDirectory = ResolveGitDirectory(gitPath, directory.FullName);
            var revision = gitDirectory is null ? null : TryReadRevisionFromGitDirectory(gitDirectory);
            if (!string.IsNullOrWhiteSpace(revision))
            {
                return ShortenRevision(revision);
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string? ResolveGitDirectory(string gitPath, string repositoryDirectory)
    {
        if (Directory.Exists(gitPath))
        {
            return gitPath;
        }

        if (!File.Exists(gitPath))
        {
            return null;
        }

        var content = File.ReadAllText(gitPath).Trim();
        const string prefix = "gitdir:";
        if (!content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = content[prefix.Length..].Trim();
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(repositoryDirectory, path));
    }

    private static string? TryReadRevisionFromGitDirectory(string gitDirectory)
    {
        var headPath = Path.Combine(gitDirectory, "HEAD");
        if (!File.Exists(headPath))
        {
            return null;
        }

        var head = File.ReadAllText(headPath).Trim();
        const string refPrefix = "ref:";
        if (!head.StartsWith(refPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return head;
        }

        var refName = head[refPrefix.Length..].Trim();
        var refPath = Path.Combine(gitDirectory, refName.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(refPath))
        {
            return File.ReadAllText(refPath).Trim();
        }

        return TryReadPackedRef(gitDirectory, refName);
    }

    private static string? TryReadPackedRef(string gitDirectory, string refName)
    {
        var packedRefsPath = Path.Combine(gitDirectory, "packed-refs");
        if (!File.Exists(packedRefsPath))
        {
            return null;
        }

        foreach (var line in File.ReadLines(packedRefsPath))
        {
            if (line.Length == 0 || line[0] == '#' || line[0] == '^')
            {
                continue;
            }

            var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && string.Equals(parts[1], refName, StringComparison.Ordinal))
            {
                return parts[0];
            }
        }

        return null;
    }

    private static string ShortenRevision(string revision)
    {
        var trimmed = revision.Trim();
        return trimmed.Length > 12 ? trimmed[..12] : trimmed;
    }
}
