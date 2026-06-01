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
    private readonly AutomationSettingsService _settingsService;
    private readonly AutomationRunLogService _runLogService;
    private readonly FolderOpenService _folderOpenService;
    private CancellationTokenSource? _automationCancellation;
    private string? _currentRunLogPath;

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
    private string chromePath = string.Empty;

    [ObservableProperty]
    private string chromeProfileDirectory = "Default";

    [ObservableProperty]
    private string remoteDebuggingPortText = "9222";

    [ObservableProperty]
    private string stepTimeoutSecondsText = "12";

    [ObservableProperty]
    private string chromeStatus = "Chrome 연결을 확인하지 않았습니다.";

    [ObservableProperty]
    private string statusMessage = "대기 중";

    [ObservableProperty]
    private string lastRunLogPath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunAutomationCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelAutomationCommand))]
    private bool isRunning;

    public MainWindowViewModel(
        ChromeConnectionService chromeConnectionService,
        ErpAutomationService erpAutomationService,
        AutomationSettingsService settingsService,
        AutomationRunLogService runLogService,
        FolderOpenService folderOpenService)
    {
        _chromeConnectionService = chromeConnectionService;
        _erpAutomationService = erpAutomationService;
        _settingsService = settingsService;
        _runLogService = runLogService;
        _folderOpenService = folderOpenService;

        var settings = _settingsService.Load();
        ChromePath = settings.ChromePath;
        ChromeProfileDirectory = settings.ChromeProfileDirectory;
        RemoteDebuggingPortText = settings.RemoteDebuggingPort.ToString();
        StepTimeoutSecondsText = settings.StepTimeoutSeconds.ToString();
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
        if (!TryCreateSettings(out var settings, out var error))
        {
            AddWarning(error);
            StatusMessage = "Chrome 설정 확인 필요";
            return;
        }

        AddInfo("Chrome 연결을 확인합니다.");
        var result = await _chromeConnectionService.CheckConnectionAsync(settings, CancellationToken.None);
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
        if (!TryCreateSettings(out var settings, out var error))
        {
            AddWarning(error);
            StatusMessage = "Chrome 설정 확인 필요";
            return;
        }

        try
        {
            _chromeConnectionService.StartChromeWithRemoteDebugging(settings);
            AddInfo($"Chrome을 원격 디버깅 포트 {settings.RemoteDebuggingPort}로 실행했습니다.");
            StatusMessage = "Chrome 실행 완료";
        }
        catch (Exception ex)
        {
            AddError(ex.Message);
            StatusMessage = "Chrome 실행 실패";
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        if (!TryCreateSettings(out var settings, out var error))
        {
            AddWarning(error);
            StatusMessage = "Chrome 설정 저장 실패";
            return;
        }

        _settingsService.Save(settings);
        AddInfo($"Chrome 설정을 저장했습니다: {_settingsService.SettingsPath}");
        StatusMessage = "Chrome 설정 저장 완료";
    }

    [RelayCommand]
    private void OpenRunLogFolder()
    {
        OpenFolder(_runLogService.LogDirectory, "실행 로그 폴더");
    }

    [RelayCommand]
    private void OpenFailureFolder()
    {
        OpenFolder(ErpAutomationService.FailureDirectory, "실패 자료 폴더");
    }

    [RelayCommand(CanExecute = nameof(CanRunAutomation))]
    private async Task RunAutomationAsync()
    {
        TransactionDate = DateTime.Today;

        if (!TryCreateInput(out var input, out var error))
        {
            AddWarning(error);
            StatusMessage = "입력값 확인 필요";
            return;
        }

        if (!TryCreateSettings(out var settings, out error))
        {
            AddWarning(error);
            StatusMessage = "Chrome 설정 확인 필요";
            return;
        }

        _automationCancellation?.Dispose();
        _automationCancellation = new CancellationTokenSource();
        _currentRunLogPath = _runLogService.StartRun(input, settings);
        LastRunLogPath = $"로그 파일: {_currentRunLogPath}";

        AddInfo($"실행 로그 파일을 생성했습니다: {_currentRunLogPath}");
        AddInfo($"입력 확인: 거래일자 {input.TransactionDateText}, 수량 {input.QuantityText}, 단가 {input.UnitPriceText}");
        AddInfo($"예상 공급가액 {input.SupplyAmountText}, 예상 세액 {input.TaxAmountText}");

        IsRunning = true;
        StatusMessage = "자동화 실행 중";

        try
        {
            AddInfo("[01/30] Chrome 연결을 확인합니다.");
            var chromeConnection = await _chromeConnectionService.CheckConnectionAsync(settings, _automationCancellation.Token);
            ChromeStatus = chromeConnection.Message;
            if (!chromeConnection.IsConnected)
            {
                AddWarning($"[01/30] {chromeConnection.Message}");
                StatusMessage = "Chrome 연결 필요";
                return;
            }

            AddInfo($"[01/30] {chromeConnection.Message}");

            var progress = new Progress<AutomationProgress>(OnAutomationProgress);
            await _erpAutomationService.RunAsync(input, settings, progress, _automationCancellation.Token);
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
            if (!string.IsNullOrWhiteSpace(_currentRunLogPath))
            {
                AddInfo($"실행 로그 저장 완료: {_currentRunLogPath}");
            }

            _currentRunLogPath = null;
            IsRunning = false;
        }
    }

    private bool TryCreateSettings(out AutomationSettings settings, out string error)
    {
        settings = new AutomationSettings
        {
            ChromePath = (ChromePath ?? string.Empty).Trim(),
            ChromeProfileDirectory = string.IsNullOrWhiteSpace(ChromeProfileDirectory)
                ? "Default"
                : ChromeProfileDirectory.Trim()
        };

        if (!int.TryParse(RemoteDebuggingPortText, out var port) || port < 1 || port > 65535)
        {
            error = "원격 디버깅 포트는 1부터 65535 사이의 숫자로 입력해야 합니다.";
            return false;
        }

        settings.RemoteDebuggingPort = port;

        if (!int.TryParse(StepTimeoutSecondsText, out var timeoutSeconds) || timeoutSeconds < 3 || timeoutSeconds > 120)
        {
            error = "단계 대기 시간은 3부터 120 사이의 초 단위 숫자로 입력해야 합니다.";
            return false;
        }

        settings.StepTimeoutSeconds = timeoutSeconds;
        error = string.Empty;
        return true;
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
        var entry = new AutomationLogEntry(DateTime.Now, level, message);
        Logs.Insert(0, entry);

        if (!string.IsNullOrWhiteSpace(_currentRunLogPath))
        {
            try
            {
                _runLogService.Append(_currentRunLogPath, entry);
            }
            catch
            {
                // 화면 로그는 유지하고 파일 로그 저장 실패만 무시한다.
            }
        }
    }

    private void OpenFolder(string path, string name)
    {
        try
        {
            _folderOpenService.OpenFolder(path);
            AddInfo($"{name}를 열었습니다: {path}");
            StatusMessage = $"{name} 열기 완료";
        }
        catch (Exception ex)
        {
            AddError($"{name} 열기 실패: {ex.Message}");
            StatusMessage = $"{name} 열기 실패";
        }
    }
}
