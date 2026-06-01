namespace AutoTest.ErpAutomation.Services;

public enum AutomationLogLevel
{
    Info,
    Warning,
    Error
}

public sealed record AutomationProgress(AutomationLogLevel Level, string Message)
{
    public static AutomationProgress Info(string message) => new(AutomationLogLevel.Info, message);

    public static AutomationProgress Warning(string message) => new(AutomationLogLevel.Warning, message);

    public static AutomationProgress Error(string message) => new(AutomationLogLevel.Error, message);
}
