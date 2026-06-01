using System.Collections.ObjectModel;
using AutoTest.ErpAutomation.Models;
using AutoTest.ErpAutomation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoTest.ErpAutomation.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ChromeConnectionService _chromeConnectionService;
    private readonly ErpAutomationService _erpAutomationService;
    private CancellationTokenSource? _automationCancellation;

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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunAutomationCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelAutomationCommand))]
    private bool isRunning;

    public MainWindowViewModel(ChromeConnectionService chromeConnectionService, ErpAutomationService erpAutomationService)
    {
        _chromeConnectionService = chromeConnectionService;
        _erpAutomationService = erpAutomationService;
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

    [RelayCommand(CanExecute = nameof(CanRunAutomation))]
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

        _automationCancellation?.Dispose();
        _automationCancellation = new CancellationTokenSource();
        IsRunning = true;
        StatusMessage = "자동화 실행 중";

        try
        {
            var progress = new Progress<AutomationProgress>(OnAutomationProgress);
            await _erpAutomationService.RunAsync(input, progress, _automationCancellation.Token);
            StatusMessage = "자동화 완료";
        }
        catch (OperationCanceledException)
        {
            AddWarning("자동화가 중지되었습니다.");
            StatusMessage = "자동화 중지";
        }
        catch (Exception ex)
        {
            AddError(ex.Message);
            StatusMessage = "자동화 실패";
        }
        finally
        {
            IsRunning = false;
        }
    }

    private bool CanRunAutomation()
    {
        return !IsRunning;
    }

    [RelayCommand(CanExecute = nameof(CanCancelAutomation))]
    private void CancelAutomation()
    {
        _automationCancellation?.Cancel();
    }

    private bool CanCancelAutomation()
    {
        return IsRunning;
    }

    private void OnAutomationProgress(AutomationProgress progress)
    {
        switch (progress.Level)
        {
            case AutomationLogLevel.Warning:
                AddWarning(progress.Message);
                break;
            case AutomationLogLevel.Error:
                AddError(progress.Message);
                break;
            default:
                AddInfo(progress.Message);
                break;
        }
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
