using System.IO;
using System.IO.Compression;

namespace AutoTest.ErpAutomation.Services;

public sealed class AutomationVerificationPackageService
{
    public string PackageDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AutoTest.ErpAutomation",
        "VerificationPackages");

    public string CreatePackage(
        string runLogDirectory,
        string verificationReportDirectory,
        string failureDirectory,
        string? currentRunLogPath,
        string? currentVerificationReportPath)
    {
        Directory.CreateDirectory(PackageDirectory);

        var packagePath = CreatePackagePath(DateTime.Now);
        var files = new List<PackageFile>();

        AddKnownFile(files, currentRunLogPath, "RunLogs");
        AddKnownFile(files, currentVerificationReportPath, "VerificationReports");
        AddLatestFile(files, runLogDirectory, "erp_run_*.log", "RunLogs");
        AddLatestFile(files, verificationReportDirectory, "erp_verification_*.md", "VerificationReports");
        AddLatestFile(files, failureDirectory, "erp_failure_*.html", "Failures");
        AddLatestFile(files, failureDirectory, "erp_failure_*.png", "Failures");

        var distinctFiles = files
            .GroupBy(file => Path.GetFullPath(file.SourcePath), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        if (distinctFiles.Length == 0)
        {
            throw new FileNotFoundException("압축할 실행 로그, 검증 리포트, 실패 자료를 찾지 못했습니다.");
        }

        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
        foreach (var file in distinctFiles)
        {
            archive.CreateEntryFromFile(file.SourcePath, file.EntryName, CompressionLevel.Optimal);
        }

        return packagePath;
    }

    private string CreatePackagePath(DateTime timestamp)
    {
        var baseName = $"erp_verification_package_{timestamp:yyyyMMdd_HHmmss_fff}";
        var path = Path.Combine(PackageDirectory, $"{baseName}.zip");
        var suffix = 1;

        while (File.Exists(path))
        {
            path = Path.Combine(PackageDirectory, $"{baseName}_{suffix}.zip");
            suffix++;
        }

        return path;
    }

    private static void AddKnownFile(ICollection<PackageFile> files, string? path, string entryDirectory)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        files.Add(new PackageFile(path, CreateEntryName(entryDirectory, path)));
    }

    private static void AddLatestFile(ICollection<PackageFile> files, string directory, string searchPattern, string entryDirectory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        var latestFile = Directory.EnumerateFiles(directory, searchPattern)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault();

        if (latestFile is null)
        {
            return;
        }

        files.Add(new PackageFile(latestFile.FullName, CreateEntryName(entryDirectory, latestFile.FullName)));
    }

    private static string CreateEntryName(string entryDirectory, string path)
    {
        return $"{entryDirectory}/{Path.GetFileName(path)}";
    }

    private sealed record PackageFile(string SourcePath, string EntryName);
}
