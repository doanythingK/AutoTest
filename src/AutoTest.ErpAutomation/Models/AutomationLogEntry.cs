namespace AutoTest.ErpAutomation.Models;

public sealed record AutomationLogEntry(DateTime Time, string Level, string Message)
{
    public string TimeText => Time.ToString("HH:mm:ss");
}
