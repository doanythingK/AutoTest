using System.Diagnostics;
using System.IO;

namespace AutoTest.ErpAutomation.Services;

public sealed class FolderOpenService
{
    public void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("폴더 경로가 비어 있습니다.", nameof(path));
        }

        Directory.CreateDirectory(path);

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    public void OpenFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("파일 경로가 비어 있습니다.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"파일을 찾을 수 없습니다: {path}", path);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    public string OpenLatestFile(string directory, string searchPattern)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("폴더 경로가 비어 있습니다.", nameof(directory));
        }

        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"폴더를 찾을 수 없습니다: {directory}");
        }

        var latestFile = Directory.EnumerateFiles(directory, searchPattern)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault()
            ?? throw new FileNotFoundException($"대상 파일을 찾을 수 없습니다: {Path.Combine(directory, searchPattern)}");

        OpenFile(latestFile.FullName);
        return latestFile.FullName;
    }
}
