namespace AutoTest.ErpAutomation.Models;

public sealed class AutomationSettings
{
    public string ChromePath { get; set; } = string.Empty;

    public string ChromeProfileDirectory { get; set; } = "Default";

    public int RemoteDebuggingPort { get; set; } = 9222;

    public int StepTimeoutSeconds { get; set; } = 12;

    public string DebugEndpoint => $"http://127.0.0.1:{RemoteDebuggingPort}";

    public AutomationSettings Clone()
    {
        return new AutomationSettings
        {
            ChromePath = ChromePath,
            ChromeProfileDirectory = ChromeProfileDirectory,
            RemoteDebuggingPort = RemoteDebuggingPort,
            StepTimeoutSeconds = StepTimeoutSeconds
        };
    }
}
