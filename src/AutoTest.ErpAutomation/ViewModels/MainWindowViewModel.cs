using System.Collections.ObjectModel;
using AutoTest.ErpAutomation.Models;
using AutoTest.ErpAutomation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoTest.ErpAutomation.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ChromeConnectionService _chromeConnectionService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpectedSupplyAmountText))]
    [NotifyPropertyChangedFor(nameof(ExpectedTaxAmountText))]
    private string quantityText = "15000";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpectedSupplyAmountText))]
    [NotifyPropertyChangedFor(nameof(ExpectedTaxAmountText))]
    private string unitPriceText = "275";

    [ObservableProperty]
    private string clientCode = string.Empty;

    [ObservableProperty]
    private string creditAccountCode = string.Empty;

    [ObservableProperty]
    private DateTime? transactionDate = DateTime.Today;

    [ObservableProperty]
    private string chromeStatus = "Chrome 연결을 확인하지 않았습니다.";

    [ObservableProperty]
    private string statusMessage = "대기 중";

    public MainWindowViewModel(ChromeConnectionService chromeConnectionService)
    {
        _chromeConnectionService = chromeConnectionService;
    }

    public ObservableCollection<AutomationLogEntry> Logs { get; } = new();

    public string ExpectedSupplyAmountText => TryCreateInput(out var input)
        ? input.SupplyAmountText
        : "-";

    public string ExpectedTaxAmountText => TryCreateInput(out var input)
        ? input.TaxAmountText
        : "-";

    [RelayCommand]
    private async Task CheckChromeAsync()
    {
        AddInfo("Chrome 연결을 확인합니다.");
        var result = await _chromeConnectionService.CheckConnectionAsync(CancellationToken.None);
        ChromeStatus = result.Message;

        if (result.IsConnected)
        {
            AddInfo(result.Message);
            StatusMessage = "Chrome 연결 확인 완료";
            return;
        }

        AddWarning(result.Message);
        StatusMessage = "Chrome 연결 필요";
    }

    [RelayCommand]
    private void StartChrome()
    {
        try
        {
            _chromeConnectionService.StartChromeWithRemoteDebugging();
            AddInfo("Chrome을 원격 디버깅 포트 9222로 실행했습니다.");
            StatusMessage = "Chrome 실행 완료";
        }
        catch (Exception ex)
        {
            AddError(ex.Message);
            StatusMessage = "Chrome 실행 실패";
        }
    }

    [RelayCommand]
    private async Task RunAutomationAsync()
    {
        if (!TryCreateInput(out var input, out var error))
        {
            AddWarning(error);
            StatusMessage = "입력값 확인 필요";
            return;
        }

        AddInfo($"입력 확인: 거래일자 {input.TransactionDateText}, 수량 {input.QuantityText}, 단가 {input.UnitPriceText}");
        AddInfo($"예상 공급가액 {input.SupplyAmountText}, 예상 세액 {input.TaxAmountText}");
        await CheckChromeAsync();
        AddWarning("ERP 자동화 실행 서비스는 다음 기능 커밋에서 연결됩니다.");
    }

    private bool TryCreateInput(out AutomationInput input)
    {
        return TryCreateInput(out input, out _);
    }

    private bool TryCreateInput(out AutomationInput input, out string error)
    {
        if (AutomationInput.TryParse(
            QuantityText,
            UnitPriceText,
            ClientCode,
            CreditAccountCode,
            TransactionDate,
            out var parsed,
            out error))
        {
            input = parsed;
            return true;
        }

        input = new AutomationInput(0, 0, string.Empty, string.Empty, DateOnly.FromDateTime(DateTime.Today));
        return false;
    }

    private void AddInfo(string message)
    {
        AddLog("정보", message);
    }

    private void AddWarning(string message)
    {
        AddLog("주의", message);
    }

    private void AddError(string message)
    {
        AddLog("오류", message);
    }

    private void AddLog(string level, string message)
    {
        Logs.Insert(0, new AutomationLogEntry(DateTime.Now, level, message));
    }
}
