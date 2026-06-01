using System.IO;
using System.Text;
using AutoTest.ErpAutomation.Models;

namespace AutoTest.ErpAutomation.Services;

public sealed class AutomationVerificationReportService
{
    private static readonly IReadOnlyList<string> StepDescriptions = new[]
    {
        "Chrome 연결 확인",
        "ERP 로그인 페이지 접속",
        "아이디/비밀번호 미입력",
        "로그인 버튼만 클릭",
        "로그인 성공 확인",
        "로그인 탭 유지",
        "회계관리 클릭",
        "거래전표 메뉴 펼침",
        "거래전표(매출등록) 메뉴 펼침",
        "원화 클릭",
        "오늘 날짜 입력",
        "차변 외상매출금 [1141] 선택",
        "매출구분 폐차처리업 선택",
        "거래처코드/명 입력 후 Enter",
        "담당부서 20 입력 후 Enter",
        "전자세금계산서 발송구분 국세청HTS 선택",
        "품목명 차피 압축 입력",
        "수량 입력",
        "단가 입력",
        "계산 클릭",
        "공급가액/세액 반영 확인",
        "계정코드(대변) 입력 후 Enter",
        "라인저장 전 확인 후 클릭",
        "라인 목록 반영 확인",
        "거래전기 클릭",
        "전기 완료 상태 확인",
        "회계전표 동일자생성 클릭",
        "회계전표입력 화면 이동 확인",
        "원장전기 클릭",
        "원장전기 완료 상태 확인"
    };

    public string ReportDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AutoTest.ErpAutomation",
        "VerificationReports");

    public string CreateReport(
        AutomationInput input,
        AutomationSettings settings,
        string runResult,
        string runLogPath,
        IReadOnlyCollection<AutomationLogEntry> logs)
    {
        Directory.CreateDirectory(ReportDirectory);

        var path = CreateReportPath(DateTime.Now);
        var builder = new StringBuilder();
        builder.AppendLine("# ERP 매출등록 자동화 실행 검증 리포트");
        builder.AppendLine();
        builder.AppendLine("## 실행 정보");
        builder.AppendLine();
        builder.AppendLine($"- 생성시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"- 최종 결과: {EscapeMarkdown(runResult)}");
        builder.AppendLine($"- 앱 버전: {EscapeMarkdown(AppBuildInfoService.GetAppVersion())}");
        builder.AppendLine($"- 소스 커밋: {EscapeMarkdown(AppBuildInfoService.GetSourceRevision())}");
        builder.AppendLine($"- 실행 PC: {EscapeMarkdown(Environment.MachineName)}");
        builder.AppendLine($"- 실행 사용자: {EscapeMarkdown(Environment.UserName)}");
        builder.AppendLine($"- 실행 로그: `{EscapeMarkdown(runLogPath)}`");
        builder.AppendLine($"- Chrome 원격 디버깅 포트: {settings.RemoteDebuggingPort}");
        builder.AppendLine($"- Chrome 프로필: {EscapeMarkdown(settings.ChromeProfileDirectory)}");
        builder.AppendLine($"- 단계 대기 시간: {settings.StepTimeoutSeconds}초");
        builder.AppendLine();

        builder.AppendLine("## 입력값과 예상 계산값");
        builder.AppendLine();
        builder.AppendLine($"- 거래일자: {input.TransactionDateText}");
        builder.AppendLine($"- 수량: {input.QuantityText}");
        builder.AppendLine($"- 단가: {input.UnitPriceText}");
        builder.AppendLine($"- 거래처코드: {EscapeMarkdown(input.ClientCode)}");
        builder.AppendLine($"- 계정코드: {EscapeMarkdown(input.CreditAccountCode)}");
        builder.AppendLine($"- 예상 공급가액: {input.SupplyAmountText}");
        builder.AppendLine($"- 예상 세액: {input.TaxAmountText}");
        builder.AppendLine();

        builder.AppendLine("## 30단계 실행 대조");
        builder.AppendLine();
        builder.AppendLine("| 단계 | 요구사항 | 상태 | 근거 로그 |");
        builder.AppendLine("| --- | --- | --- | --- |");

        for (var index = 0; index < StepDescriptions.Count; index++)
        {
            var stepNumber = index + 1;
            var marker = $"[{stepNumber:00}/30]";
            var stepLogs = logs.Where(entry => entry.Message.Contains(marker, StringComparison.Ordinal)).ToArray();
            var error = stepLogs.LastOrDefault(entry => entry.Level == "오류");
            var evidence = error ?? stepLogs.LastOrDefault();
            var status = error is not null
                ? "실패"
                : stepLogs.Length > 0
                    ? "로그 확인"
                    : "미확인";

            builder.AppendLine($"| {stepNumber} | {EscapeMarkdown(StepDescriptions[index])} | {status} | {EscapeTableCell(evidence?.Message ?? "-")} |");
        }

        builder.AppendLine();
        builder.AppendLine("## 완료 판정 기준");
        builder.AppendLine();
        builder.AppendLine("- 최종 결과가 `완료`인지 확인한다.");
        builder.AppendLine("- 30단계 상태가 모두 `로그 확인`인지 확인한다.");
        builder.AppendLine("- ERP 화면에서 공급가액, 세액, 라인 목록, 거래전기 완료, 회계전표입력 이동, 원장전기 완료 상태를 함께 확인한다.");
        builder.AppendLine("- 이 리포트는 실행 로그를 자동 대조한 자료이며 실제 ERP 화면 확인을 대체하지 않는다.");

        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        return path;
    }

    private string CreateReportPath(DateTime timestamp)
    {
        var baseName = $"erp_verification_{timestamp:yyyyMMdd_HHmmss_fff}";
        var path = Path.Combine(ReportDirectory, $"{baseName}.md");
        var suffix = 1;

        while (File.Exists(path))
        {
            path = Path.Combine(ReportDirectory, $"{baseName}_{suffix}.md");
            suffix++;
        }

        return path;
    }

    private static string EscapeTableCell(string value)
    {
        return EscapeMarkdown(value).Replace("|", "\\|");
    }

    private static string EscapeMarkdown(string value)
    {
        return value.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
