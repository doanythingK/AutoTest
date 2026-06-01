using System.IO;
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

        var path = Path.Combine(LogDirectory, $"erp_run_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        var builder = new StringBuilder();
        builder.AppendLine("ERP 매출등록 자동화 실행 로그");
        builder.AppendLine($"시작시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"거래일자: {input.TransactionDateText}");
        builder.AppendLine($"수량: {input.QuantityText}");
        builder.AppendLine($"단가: {input.UnitPriceText}");
        builder.AppendLine($"거래처코드: {input.ClientCode}");
        builder.AppendLine($"계정코드: {input.CreditAccountCode}");
        builder.AppendLine($"예상 공급가액: {input.SupplyAmountText}");
        builder.AppendLine($"예상 세액: {input.TaxAmountText}");
        builder.AppendLine($"Chrome 디버깅 주소: {settings.DebugEndpoint}");
        builder.AppendLine($"Chrome 프로필: {settings.ChromeProfileDirectory}");
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
}
