using System.IO;
using System.Reflection;
using System.Text;
using AutoTest.ErpAutomation.Models;

namespace AutoTest.ErpAutomation.Services;

public sealed class AutomationRunLogService
{
    public string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AutoTest.ErpAutomation",
        "RunLogs");

    public string StartRun(AutomationInput input, AutomationSettings settings)
    {
        Directory.CreateDirectory(LogDirectory);

        var path = CreateRunLogPath(DateTime.Now);
        var builder = new StringBuilder();
        builder.AppendLine("ERP 매출등록 자동화 실행 로그");
        builder.AppendLine($"시작시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"앱 버전: {GetAppVersion()}");
        builder.AppendLine($"거래일자: {input.TransactionDateText}");
        builder.AppendLine($"수량: {input.QuantityText}");
        builder.AppendLine($"단가: {input.UnitPriceText}");
        builder.AppendLine($"거래처코드: {input.ClientCode}");
        builder.AppendLine($"계정코드: {input.CreditAccountCode}");
        builder.AppendLine($"예상 공급가액: {input.SupplyAmountText}");
        builder.AppendLine($"예상 세액: {input.TaxAmountText}");
        builder.AppendLine($"실행 PC: {Environment.MachineName}");
        builder.AppendLine($"실행 사용자: {Environment.UserName}");
        builder.AppendLine($"Chrome 경로: {FormatOptional(settings.ChromePath)}");
        builder.AppendLine($"Chrome 원격 디버깅 포트: {settings.RemoteDebuggingPort}");
        builder.AppendLine($"Chrome 디버깅 주소: {settings.DebugEndpoint}");
        builder.AppendLine($"Chrome 프로필: {settings.ChromeProfileDirectory}");
        builder.AppendLine($"단계 대기 시간: {settings.StepTimeoutSeconds}초");
        builder.AppendLine(new string('-', 80));

        File.WriteAllText(path, builder.ToString());
        return path;
    }

    public void Append(string path, AutomationLogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        File.AppendAllText(
            path,
            $"{entry.Time:yyyy-MM-dd HH:mm:ss}\t{entry.Level}\t{entry.Message}{Environment.NewLine}");
    }

    private static string FormatOptional(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "(자동 탐색)"
            : value;
    }

    private static string GetAppVersion()
    {
        var assembly = typeof(AutomationRunLogService).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "(알 수 없음)";
    }

    private string CreateRunLogPath(DateTime timestamp)
    {
        var baseName = $"erp_run_{timestamp:yyyyMMdd_HHmmss_fff}";
        var path = Path.Combine(LogDirectory, $"{baseName}.log");
        var suffix = 1;

        while (File.Exists(path))
        {
            path = Path.Combine(LogDirectory, $"{baseName}_{suffix}.log");
            suffix++;
        }

        return path;
    }
}
