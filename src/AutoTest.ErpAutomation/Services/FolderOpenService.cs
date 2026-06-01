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
}
