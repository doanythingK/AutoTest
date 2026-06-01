using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace AutoTest.ErpAutomation.Services;

public sealed class ChromeConnectionService
{
    public const string DebugEndpoint = "http://127.0.0.1:9222";

    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(2);

    private readonly HttpClient _httpClient = new()
    {
        Timeout = CheckTimeout
    };

    public async Task<ChromeConnectionResult> CheckConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"{DebugEndpoint}/json/version", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ChromeConnectionResult.Fail($"Chrome CDP 응답 오류: {(int)response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            var browser = root.TryGetProperty("Browser", out var browserProperty)
                ? browserProperty.GetString() ?? "Chrome"
                : "Chrome";
            var webSocketUrl = root.TryGetProperty("webSocketDebuggerUrl", out var wsProperty)
                ? wsProperty.GetString()
                : null;

            return string.IsNullOrWhiteSpace(webSocketUrl)
                ? ChromeConnectionResult.Fail("Chrome은 응답했지만 webSocketDebuggerUrl이 없습니다.")
                : ChromeConnectionResult.Success(browser, webSocketUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return ChromeConnectionResult.Fail("Chrome 원격 디버깅 포트(9222)에 연결할 수 없습니다.");
        }
    }

    public Process StartChromeWithRemoteDebugging()
    {
        var chromePath = FindChromePath()
            ?? throw new FileNotFoundException("Chrome 실행 파일을 찾을 수 없습니다.");

        var startInfo = new ProcessStartInfo
        {
            FileName = chromePath,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("--remote-debugging-port=9222");
        startInfo.ArgumentList.Add("--profile-directory=Default");
        startInfo.ArgumentList.Add("https://ibcenter.co.kr/erp/erp/erplogin/erplogin_dispatch.jsp");

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Chrome 실행에 실패했습니다.");
        return process;
    }

    private static string? FindChromePath()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var x86ProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var candidates = new[]
        {
            Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(x86ProgramFiles, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}

public sealed record ChromeConnectionResult(bool IsConnected, string Message, string? Browser, string? WebSocketUrl)
{
    public static ChromeConnectionResult Success(string browser, string webSocketUrl)
    {
        return new ChromeConnectionResult(true, $"연결됨: {browser}", browser, webSocketUrl);
    }

    public static ChromeConnectionResult Fail(string message)
    {
        return new ChromeConnectionResult(false, message, null, null);
    }
}
