using AutoTest.ErpAutomation.Models;
using Microsoft.Playwright;
using System.IO;
using System.Net;
using System.Text;

namespace AutoTest.ErpAutomation.Services;

public sealed class ErpAutomationService
{
    private const string LoginUrl = "https://ibcenter.co.kr/erp/erp/erplogin/erplogin_dispatch.jsp";

    public static string FailureDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AutoTest.ErpAutomation",
        "Failures");

    public async Task RunAsync(
        AutomationInput input,
        AutomationSettings settings,
        IProgress<AutomationProgress> progress,
        CancellationToken cancellationToken)
    {
        var stepTimeout = TimeSpan.FromSeconds(settings.StepTimeoutSeconds);

        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.ConnectOverCDPAsync(settings.DebugEndpoint);
        var context = browser.Contexts.FirstOrDefault()
            ?? throw new InvalidOperationException("연결된 Chrome 컨텍스트를 찾을 수 없습니다.");

        var page = await GetOrCreatePageAsync(context);
        page.SetDefaultTimeout((float)stepTimeout.TotalMilliseconds);
        var dialogHandlers = new List<(IPage Page, EventHandler<IDialog> Handler)>();
        AttachDialogHandler(page, progress, dialogHandlers);
        var loginSuccessTexts = new[] { "회계관리", "로그아웃" };

        try
        {
            await StepAsync(progress, "[02/30] ERP 로그인 페이지에 접속합니다.", async () =>
            {
                await page.GotoAsync(LoginUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = (float)stepTimeout.TotalMilliseconds
                });
            });

            progress.Report(AutomationProgress.Info("[03/30] 아이디와 비밀번호 입력값은 건드리지 않습니다."));

            await StepAsync(progress, "[04/30] 로그인 버튼만 클릭합니다.", async () =>
            {
                await WaitForLoginPageSettleAsync(page, stepTimeout, cancellationToken);

                if (await PageContainsAnyAsync(page, loginSuccessTexts, cancellationToken))
                {
                    progress.Report(AutomationProgress.Info("이미 로그인된 화면으로 판단되어 로그인 버튼 클릭을 생략합니다."));
                    return;
                }

                await ClickTextAsync(page, "로그인", stepTimeout, cancellationToken);
            });

            await StepAsync(progress, "[05/30] 로그인 성공 여부를 확인합니다.", async () =>
            {
                page = await WaitUntilLoginSuccessAsync(context, page, loginSuccessTexts, stepTimeout, progress, cancellationToken);
                page.SetDefaultTimeout((float)stepTimeout.TotalMilliseconds);
                AttachDialogHandler(page, progress, dialogHandlers);
            });

            progress.Report(AutomationProgress.Info("[06/30] 로그인된 탭은 닫지 않고 유지합니다."));

            await StepAsync(progress, "[07/30] 회계관리 버튼을 클릭합니다.", () => ClickTextAsync(page, "회계관리", stepTimeout, cancellationToken));
            await StepAsync(progress, "[08/30] 거래전표 메뉴를 펼칩니다.", () => ClickTextAsync(page, "거래전표", stepTimeout, cancellationToken));
            await StepAsync(progress, "[09/30] 거래전표(매출등록) 메뉴를 펼칩니다.", () => ClickTextAsync(page, "거래전표(매출등록)", stepTimeout, cancellationToken));
            await StepAsync(progress, "[10/30] 원화 버튼을 클릭합니다.", () => ClickTextAsync(page, "원화", stepTimeout, cancellationToken));

            await StepAsync(progress, $"[11/30] 거래일자에 오늘 날짜({input.TransactionDateText})를 입력합니다.", () =>
                FillNearAnyLabelAsync(page, new[] { "거래일자", "거래 일자", "일자" }, input.TransactionDateCandidates, pressEnter: false, stepTimeout, cancellationToken));

            await StepAsync(progress, "[12/30] 차변에서 외상매출금 [1141]을 선택합니다.", async () =>
            {
                await SelectByAnyLabelAsync(page, new[] { "차변", "차변계정", "차변 계정" }, new[] { "외상매출금 [1141]", "외상매출금" }, stepTimeout, cancellationToken);
            });

            await StepAsync(progress, "[13/30] 매출구분에서 서비스(사회및개인)업 폐차처리업을 선택합니다.", async () =>
            {
                await SelectByAnyLabelAsync(page, new[] { "매출구분", "매출 구분", "구분" }, new[] { "서비스(사회및개인)업 폐차처리업", "폐차처리업" }, stepTimeout, cancellationToken);
            });

            await StepAsync(progress, $"[14/30] 거래처코드/명에 {input.ClientCode} 값을 입력하고 Enter를 실행합니다.", () =>
                FillNearAnyLabelAsync(page, new[] { "거래처코드/명", "거래처코드", "거래처명" }, input.ClientCode, pressEnter: true, stepTimeout, cancellationToken));

            await StepAsync(progress, "[15/30] 담당부서에 20을 입력하고 Enter를 실행합니다.", () =>
                FillNearAnyLabelAsync(page, new[] { "담당부서", "부서" }, "20", pressEnter: true, stepTimeout, cancellationToken));

            await StepAsync(progress, "[16/30] 전자(세금)계산서 발송구분에서 국세청HTS를 선택합니다.", async () =>
            {
                await SelectByAnyLabelAsync(page, new[] { "전자(세금)계산서 발송구분", "전자세금계산서 발송구분", "발송구분" }, new[] { "국세청HTS", "국세청" }, stepTimeout, cancellationToken);
            });

            await StepAsync(progress, $"[17/30] 품목코드/품목명(적요)에 {AutomationInput.ItemText}를 입력합니다.", () =>
                FillNearAnyLabelAsync(page, new[] { "품목코드/품목명(적요)", "품목코드/품목명", "품목명(적요)", "적요" }, AutomationInput.ItemText, pressEnter: false, stepTimeout, cancellationToken, preferLowerArea: true, preferWideControl: true));

            await StepAsync(progress, $"[18/30] 수량에 {input.QuantityText} 값을 입력합니다.", () =>
                FillNearLabelAsync(page, "수량", input.QuantityInputCandidates, pressEnter: false, stepTimeout, cancellationToken, preferLowerArea: true));

            await StepAsync(progress, $"[19/30] 단가에 {input.UnitPriceText} 값을 입력합니다.", () =>
                FillNearLabelAsync(page, "단가", input.UnitPriceInputCandidates, pressEnter: false, stepTimeout, cancellationToken, preferLowerArea: true));

            await StepAsync(progress, "[20/30] 계산 버튼을 클릭합니다.", () => ClickTextAsync(page, "계산", stepTimeout, cancellationToken));

            await StepAsync(progress, "[21/30] 계산 결과가 정상 반영되었는지 확인합니다.", async () =>
            {
                var calculationExpectations = new object[]
                {
                    new { label = "공급가액", values = input.SupplyAmountCandidates },
                    new { label = "세액", values = input.TaxAmountCandidates }
                };
                var hasZeroAmount = await PageHasZeroNearAnyLabelAsync(page, new[] { "공급가액", "세액" }, cancellationToken);
                var ok = await PageHasExpectedValuesNearLabelsAsync(page, calculationExpectations, cancellationToken);
                if (hasZeroAmount || !ok)
                {
                    progress.Report(AutomationProgress.Warning("공급가액/세액이 0이거나 기대값을 찾지 못했습니다. 수량과 단가를 다시 입력한 뒤 계산을 재시도합니다."));
                    await FillNearLabelAsync(page, "수량", input.QuantityInputCandidates, pressEnter: false, stepTimeout, cancellationToken, preferLowerArea: true);
                    await FillNearLabelAsync(page, "단가", input.UnitPriceInputCandidates, pressEnter: false, stepTimeout, cancellationToken, preferLowerArea: true);
                    await ClickTextAsync(page, "계산", stepTimeout, cancellationToken);
                }

                await WaitUntilExpectedValuesNearLabelsAsync(page, calculationExpectations, stepTimeout, cancellationToken);
            });

            await StepAsync(progress, $"[22/30] 계정코드(대변)에 {input.CreditAccountCode} 값을 입력하고 Enter를 실행합니다.", () =>
                FillNearAnyLabelAsync(page, new[] { "계정코드(대변)", "계정코드", "대변" }, input.CreditAccountCode, pressEnter: true, stepTimeout, cancellationToken, preferLowerArea: true));

            await StepAsync(progress, "[23/30] 라인저장 전 입력값과 계산 결과를 확인한 뒤 라인저장(L) 버튼을 클릭합니다.", async () =>
            {
                var beforeLineSaveExpectations = new object[]
                {
                    new { label = "수량", values = input.QuantityCandidates },
                    new { label = "단가", values = input.UnitPriceCandidates },
                    new { label = "공급가액", values = input.SupplyAmountCandidates },
                    new { label = "세액", values = input.TaxAmountCandidates }
                };
                await WaitUntilAnyTextAsync(page, new[] { AutomationInput.ItemText }, stepTimeout, cancellationToken);
                await WaitUntilExpectedValuesNearLabelsAsync(page, beforeLineSaveExpectations, stepTimeout, cancellationToken);
                await ClickTextAsync(page, "라인저장", stepTimeout, cancellationToken, preferLowerArea: true);
            });

            await StepAsync(progress, "[24/30] 라인 목록 반영 여부를 확인합니다.", async () =>
            {
                await WaitUntilLineResultRowAsync(page, input.LineResultGroups, stepTimeout, cancellationToken);
            });

            await StepAsync(progress, "[25/30] 거래전기[S] 버튼을 클릭합니다.", () => ClickTextAsync(page, "거래전기", stepTimeout, cancellationToken, preferUpperArea: true));
            await StepAsync(progress, "[26/30] 화면이 전기 완료 상태로 바뀌었는지 확인합니다.", () => WaitUntilAnyTextAsync(page, new[] { "전기 완료", "전기완료", "거래전기 완료", "거래전기: 완료" }, stepTimeout, cancellationToken));
            await StepAsync(progress, "[27/30] 회계전표 동일자생성 버튼을 클릭합니다.", async () =>
            {
                await ClickTextAsync(page, "회계전표 동일자생성", stepTimeout, cancellationToken, preferUpperArea: true);
                page = await RefreshActivePageAsync(context, page, new[] { "회계전표입력", "회계전표 입력" }, stepTimeout, progress, cancellationToken);
                page.SetDefaultTimeout((float)stepTimeout.TotalMilliseconds);
                AttachDialogHandler(page, progress, dialogHandlers);
            });
            await StepAsync(progress, "[28/30] 회계전표입력 화면으로 이동했는지 확인합니다.", () => WaitUntilAnyTextAsync(page, new[] { "회계전표입력", "회계전표 입력" }, stepTimeout, cancellationToken));
            await StepAsync(progress, "[29/30] 원장전기[P] 버튼을 클릭합니다.", () => ClickTextAsync(page, "원장전기", stepTimeout, cancellationToken, preferUpperArea: true));
            await StepAsync(progress, "[30/30] 원장전기 완료 상태가 표시되는지 확인합니다.", () => WaitUntilAnyTextAsync(page, new[] { "원장전기: 완료", "원장전기 완료" }, stepTimeout, cancellationToken));

            progress.Report(AutomationProgress.Info("ERP 매출등록 자동화 절차가 완료되었습니다."));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await SaveFailureArtifactsAsync(page, input, progress);
            throw;
        }
        finally
        {
            foreach (var (handlerPage, handler) in dialogHandlers)
            {
                handlerPage.Dialog -= handler;
            }
        }
    }

    private static async Task<IPage> GetOrCreatePageAsync(IBrowserContext context)
    {
        var page = context.Pages.FirstOrDefault(p => !p.IsClosed)
            ?? await context.NewPageAsync();
        await page.BringToFrontAsync();
        return page;
    }

    private static void AttachDialogHandler(
        IPage page,
        IProgress<AutomationProgress> progress,
        ICollection<(IPage Page, EventHandler<IDialog> Handler)> dialogHandlers)
    {
        if (dialogHandlers.Any(item => ReferenceEquals(item.Page, page)))
        {
            return;
        }

        async void Handler(object? _, IDialog dialog)
        {
            progress.Report(AutomationProgress.Info($"브라우저 대화상자 자동 확인: {dialog.Type} - {dialog.Message}"));
            try
            {
                await dialog.AcceptAsync();
            }
            catch (Exception ex)
            {
                progress.Report(AutomationProgress.Warning($"브라우저 대화상자 확인 실패: {ex.Message}"));
            }
        }

        page.Dialog += Handler;
        dialogHandlers.Add((page, Handler));
    }

    private static async Task StepAsync(IProgress<AutomationProgress> progress, string message, Func<Task> action)
    {
        progress.Report(AutomationProgress.Info(message));
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            progress.Report(AutomationProgress.Error($"{message} 실패: {ex.Message}"));
            throw;
        }
    }

    private static async Task ClickTextAsync(
        IPage page,
        string text,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        bool preferUpperArea = false,
        bool preferLowerArea = false)
    {
        await RunInAnyFrameAsync(
            page,
            frame => ClickTextInFrameAsync(page, frame, text, preferUpperArea, preferLowerArea),
            $"'{text}' 클릭",
            timeout,
            cancellationToken);

        await WaitAfterClickAsync(page, timeout, cancellationToken);
    }

    private static async Task FillNearLabelAsync(
        IPage page,
        string label,
        string value,
        bool pressEnter,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        bool preferLowerArea = false,
        bool preferWideControl = false)
    {
        await FillNearAnyLabelAsync(page, new[] { label }, new[] { value }, pressEnter, timeout, cancellationToken, preferLowerArea, preferWideControl);
    }

    private static async Task FillNearLabelAsync(
        IPage page,
        string label,
        IReadOnlyCollection<string> values,
        bool pressEnter,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        bool preferLowerArea = false,
        bool preferWideControl = false)
    {
        await FillNearAnyLabelAsync(page, new[] { label }, values, pressEnter, timeout, cancellationToken, preferLowerArea, preferWideControl);
    }

    private static async Task FillNearAnyLabelAsync(
        IPage page,
        IReadOnlyCollection<string> labels,
        string value,
        bool pressEnter,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        bool preferLowerArea = false,
        bool preferWideControl = false)
    {
        await FillNearAnyLabelAsync(page, labels, new[] { value }, pressEnter, timeout, cancellationToken, preferLowerArea, preferWideControl);
    }

    private static async Task FillNearAnyLabelAsync(
        IPage page,
        IReadOnlyCollection<string> labels,
        IReadOnlyCollection<string> values,
        bool pressEnter,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        bool preferLowerArea = false,
        bool preferWideControl = false)
    {
        await RunInAnyFrameAsync(
            page,
            frame => FillNearLabelInFrameAsync(frame, labels, values, preferLowerArea, preferWideControl),
            $"'{string.Join("' 또는 '", labels)}' 입력",
            timeout,
            cancellationToken);

        if (pressEnter)
        {
            await page.Keyboard.PressAsync("Enter");
            await WaitAfterEnterAsync(page, timeout, cancellationToken);
        }
    }

    private static async Task WaitAfterEnterAsync(IPage page, TimeSpan timeout, CancellationToken cancellationToken)
    {
        await Task.Delay(500, cancellationToken);
        await WaitForBusyIndicatorToClearAsync(page, timeout, cancellationToken);
    }

    private static async Task WaitAfterClickAsync(IPage page, TimeSpan timeout, CancellationToken cancellationToken)
    {
        await Task.Delay(300, cancellationToken);
        await WaitForBusyIndicatorToClearAsync(page, timeout, cancellationToken);
    }

    private static async Task WaitAfterSelectionAsync(IPage page, TimeSpan timeout, CancellationToken cancellationToken)
    {
        await Task.Delay(300, cancellationToken);
        await WaitForBusyIndicatorToClearAsync(page, timeout, cancellationToken);
    }

    private static async Task WaitForBusyIndicatorToClearAsync(IPage page, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var settleTimeout = TimeSpan.FromMilliseconds(Math.Min(timeout.TotalMilliseconds, 3000));
        var deadline = DateTime.UtcNow.Add(settleTimeout);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await PageHasBusyIndicatorAsync(page, cancellationToken))
            {
                return;
            }

            await Task.Delay(250, cancellationToken);
        }
    }

    private static async Task WaitForLoginPageSettleAsync(IPage page, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            await page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions
            {
                Timeout = (float)Math.Min(timeout.TotalMilliseconds, 3000)
            });
        }
        catch
        {
            // Some ERP pages keep loading auxiliary resources; continue after the bounded wait.
        }

        await Task.Delay(700, cancellationToken);

        var settleTimeout = TimeSpan.FromMilliseconds(Math.Min(timeout.TotalMilliseconds, 3000));
        var deadline = DateTime.UtcNow.Add(settleTimeout);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await PageHasBusyIndicatorAsync(page, cancellationToken))
            {
                return;
            }

            await Task.Delay(250, cancellationToken);
        }
    }

    private static Task SelectByLabelAsync(IPage page, string label, string optionText, TimeSpan timeout, CancellationToken cancellationToken)
    {
        return SelectByAnyLabelAsync(page, new[] { label }, new[] { optionText }, timeout, cancellationToken);
    }

    private static async Task SelectByLabelAsync(
        IPage page,
        string label,
        IReadOnlyCollection<string> optionTexts,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectByAnyLabelAsync(page, new[] { label }, optionTexts, timeout, cancellationToken);
    }

    private static async Task SelectByAnyLabelAsync(
        IPage page,
        IReadOnlyCollection<string> labels,
        IReadOnlyCollection<string> optionTexts,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var quickTimeout = TimeSpan.FromMilliseconds(Math.Min(timeout.TotalMilliseconds, 2500));
        foreach (var optionText in optionTexts)
        {
            if (await TryRunInAnyFrameAsync(
                page,
                frame => SelectNativeByLabelInFrameAsync(frame, labels, optionText),
                quickTimeout,
                cancellationToken))
            {
                await WaitAfterSelectionAsync(page, timeout, cancellationToken);
                return;
            }
        }

        await RunInAnyFrameAsync(page, frame => OpenDropdownByLabelInFrameAsync(frame, labels), $"'{string.Join("' 또는 '", labels)}' 드롭다운 열기", timeout, cancellationToken);

        foreach (var optionText in optionTexts)
        {
            if (await TryRunInAnyFrameAsync(
                page,
                frame => ClickTextInFrameAsync(page, frame, optionText),
                quickTimeout,
                cancellationToken))
            {
                await WaitAfterSelectionAsync(page, timeout, cancellationToken);
                return;
            }
        }

        throw new TimeoutException($"'{string.Join("' 또는 '", labels)}' 드롭다운에서 다음 옵션을 찾지 못했습니다: {string.Join(", ", optionTexts)}");
    }

    private static async Task WaitUntilAnyTextAsync(IPage page, IReadOnlyCollection<string> texts, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await PageContainsAnyAsync(page, texts, cancellationToken))
            {
                return;
            }

            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException($"화면에서 다음 텍스트를 찾지 못했습니다: {string.Join(", ", texts)}");
    }

    private static async Task<IPage> RefreshActivePageAsync(
        IBrowserContext context,
        IPage currentPage,
        IReadOnlyCollection<string> expectedTexts,
        TimeSpan timeout,
        IProgress<AutomationProgress> progress,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        var fallbackPage = currentPage;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pages = context.Pages.Where(candidate => !candidate.IsClosed).ToArray();
            foreach (var candidate in pages.Reverse())
            {
                if (await PageContainsAnyAsync(candidate, expectedTexts, cancellationToken))
                {
                    await candidate.BringToFrontAsync();
                    if (!ReferenceEquals(candidate, currentPage))
                    {
                        progress.Report(AutomationProgress.Info("회계전표입력 화면이 열린 탭으로 전환했습니다."));
                    }

                    return candidate;
                }
            }

            var newestPage = pages.LastOrDefault();
            if (newestPage is not null && !ReferenceEquals(newestPage, currentPage))
            {
                fallbackPage = newestPage;
            }

            await Task.Delay(350, cancellationToken);
        }

        if (!ReferenceEquals(fallbackPage, currentPage))
        {
            await fallbackPage.BringToFrontAsync();
            progress.Report(AutomationProgress.Info("새로 열린 탭으로 전환한 뒤 회계전표입력 화면 확인을 계속합니다."));
        }

        return fallbackPage;
    }

    private static async Task<IPage> WaitUntilLoginSuccessAsync(
        IBrowserContext context,
        IPage currentPage,
        IReadOnlyCollection<string> successTexts,
        TimeSpan timeout,
        IProgress<AutomationProgress> progress,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        var passwordChangeTexts = new[] { "비밀번호 변경", "비밀번호를 변경", "새 비밀번호", "현재 비밀번호", "비밀번호 재설정", "password change" };
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pages = context.Pages.Where(candidate => !candidate.IsClosed).Reverse().ToArray();
            foreach (var page in pages)
            {
                if (await PageContainsAnyAsync(page, successTexts, cancellationToken))
                {
                    await page.BringToFrontAsync();
                    if (!ReferenceEquals(page, currentPage))
                    {
                        progress.Report(AutomationProgress.Info("로그인 성공 화면이 열린 탭으로 전환했습니다."));
                    }

                    return page;
                }
            }

            foreach (var page in pages)
            {
                if (await PageContainsAnyAsync(page, passwordChangeTexts, cancellationToken))
                {
                    throw new InvalidOperationException("비밀번호 입력 또는 변경 화면이 감지되어 자동화를 중단합니다. 자동화는 아이디/비밀번호를 입력하거나 변경하지 않습니다.");
                }
            }

            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException("로그인 성공 화면을 확인하지 못했습니다.");
    }

    private static async Task WaitUntilAllGroupsAsync(
        IPage page,
        IReadOnlyCollection<IReadOnlyCollection<string>> groups,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await PageContainsAllGroupsAsync(page, groups, cancellationToken))
            {
                return;
            }

            await Task.Delay(500, cancellationToken);
        }

        var expected = groups.Select(group => $"[{string.Join(" 또는 ", group)}]");
        throw new TimeoutException($"화면에서 다음 항목 조합을 찾지 못했습니다: {string.Join(", ", expected)}");
    }

    private static async Task WaitUntilLineResultRowAsync(
        IPage page,
        IReadOnlyCollection<IReadOnlyCollection<string>> groups,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await PageContainsLineResultRowAsync(page, groups, cancellationToken))
            {
                return;
            }

            await Task.Delay(500, cancellationToken);
        }

        var expected = groups.Select(group => $"[{string.Join(" 또는 ", group)}]");
        throw new TimeoutException($"라인 목록 행에서 다음 항목 조합을 찾지 못했습니다: {string.Join(", ", expected)}");
    }

    private static async Task WaitUntilExpectedValuesNearLabelsAsync(
        IPage page,
        IReadOnlyCollection<object> expectations,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await PageHasExpectedValuesNearLabelsAsync(page, expectations, cancellationToken))
            {
                return;
            }

            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException("입력 라벨 주변에서 기대값을 찾지 못했습니다.");
    }

    private static async Task<bool> PageContainsAnyAsync(IPage page, IReadOnlyCollection<string> texts, CancellationToken cancellationToken)
    {
        foreach (var frame in page.Frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var found = await frame.EvaluateAsync<bool>(
                    @"(texts) => {
                        const visible = element => {
                            const style = window.getComputedStyle(element);
                            const rect = element.getBoundingClientRect();
                            return style && style.visibility !== 'hidden' && style.display !== 'none' && rect.width > 0 && rect.height > 0;
                        };
                        const valueText = element => {
                            const values = [
                                element.innerText,
                                element.textContent,
                                element.value,
                                element.title,
                                element.getAttribute('aria-label')
                            ];
                            if (element.tagName?.toLowerCase() === 'select') {
                                values.push(...Array.from(element.selectedOptions || []).map(option => option.text));
                            }
                            return values.filter(Boolean).join(' ');
                        };
                        const visibleValues = Array.from(document.querySelectorAll('input:not([type=hidden]), textarea, select, [title], [aria-label]'))
                            .filter(visible)
                            .map(valueText);
                        const bodyText = [document.body?.innerText || '', ...visibleValues].join(' ').replace(/\s+/g, ' ');
                        const normalizeKey = value => String(value || '').replace(/[\s:：]/g, '');
                        const bodyKey = normalizeKey(bodyText);
                        return texts.some(text => bodyText.includes(text) || bodyKey.includes(normalizeKey(text)));
                    }",
                    texts);
                if (found)
                {
                    return true;
                }
            }
            catch
            {
                // Some transient frames can disappear while the ERP screen is changing.
            }
        }

        return false;
    }

    private static async Task<bool> PageHasExpectedValuesNearLabelsAsync(
        IPage page,
        IReadOnlyCollection<object> expectations,
        CancellationToken cancellationToken)
    {
        foreach (var frame in page.Frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var found = await frame.EvaluateAsync<bool>(
                    @"(expectations) => {
                        const normalizeText = value => String(value || '').replace(/\s+/g, ' ').trim();
                        const normalizeKey = value => normalizeText(value).replace(/\s/g, '');
                        const normalizeNumber = value => String(value || '').replace(/[,\s]/g, '');
                        const visible = element => {
                            const style = window.getComputedStyle(element);
                            const rect = element.getBoundingClientRect();
                            return style && style.visibility !== 'hidden' && style.display !== 'none' && rect.width > 0 && rect.height > 0;
                        };
                        const valueOf = element => normalizeText([
                            element.value,
                            element.innerText,
                            element.textContent,
                            element.title,
                            element.getAttribute('aria-label')
                        ].filter(Boolean).join(' '));
                        const valuesNearLabel = labelElement => {
                            const values = [];
                            const row = labelElement.closest('tr');
                            if (row) {
                                values.push(...Array.from(row.querySelectorAll('input:not([type=hidden]), textarea, td, span, div'))
                                    .filter(element => element !== labelElement && visible(element))
                                    .map(valueOf));
                            }

                            const labelRect = labelElement.getBoundingClientRect();
                            values.push(...Array.from(document.querySelectorAll('input:not([type=hidden]), textarea, td, span, div'))
                                .filter(element => element !== labelElement && visible(element))
                                .map(element => ({ element, rect: element.getBoundingClientRect() }))
                                .filter(item => Math.abs(item.rect.top - labelRect.top) < 70 && item.rect.left >= labelRect.left)
                                .sort((a, b) => Math.abs(a.rect.left - labelRect.right) - Math.abs(b.rect.left - labelRect.right))
                                .slice(0, 4)
                                .map(item => valueOf(item.element)));
                            return values.filter(Boolean).map(normalizeNumber);
                        };
                        const candidates = Array.from(document.querySelectorAll('label, th, td, span, div'))
                            .filter(element => visible(element));
                        return expectations.every(expectation => {
                            const labelKey = normalizeKey(expectation.label);
                            const expectedValues = Array.from(expectation.values || []).map(normalizeNumber);
                            return candidates.some(labelElement => {
                                const candidateKey = normalizeKey(labelElement.innerText || labelElement.textContent || labelElement.title);
                                if (!candidateKey.includes(labelKey)) return false;
                                const nearbyValues = valuesNearLabel(labelElement);
                                return expectedValues.some(expected => nearbyValues.some(value => value.includes(expected)));
                            });
                        });
                    }",
                    expectations);
                if (found)
                {
                    return true;
                }
            }
            catch
            {
                // Some transient frames can disappear while the ERP screen is changing.
            }
        }

        return false;
    }

    private static async Task<bool> PageContainsAllGroupsAsync(
        IPage page,
        IReadOnlyCollection<IReadOnlyCollection<string>> groups,
        CancellationToken cancellationToken)
    {
        foreach (var frame in page.Frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var found = await frame.EvaluateAsync<bool>(
                    @"(groups) => {
                        const visible = element => {
                            const style = window.getComputedStyle(element);
                            const rect = element.getBoundingClientRect();
                            return style && style.visibility !== 'hidden' && style.display !== 'none' && rect.width > 0 && rect.height > 0;
                        };
                        const valueText = element => {
                            const values = [
                                element.innerText,
                                element.textContent,
                                element.value,
                                element.title,
                                element.getAttribute('aria-label')
                            ];
                            if (element.tagName?.toLowerCase() === 'select') {
                                values.push(...Array.from(element.selectedOptions || []).map(option => option.text));
                            }
                            return values.filter(Boolean).join(' ');
                        };
                        const visibleValues = Array.from(document.querySelectorAll('input:not([type=hidden]), textarea, select, [title], [aria-label]'))
                            .filter(visible)
                            .map(valueText);
                        const bodyText = [document.body?.innerText || '', ...visibleValues].join(' ').replace(/\s+/g, ' ');
                        const normalizedBodyText = bodyText.replace(/[,\s]/g, '');
                        const normalize = value => String(value || '').replace(/[,\s]/g, '');
                        return groups.every(group => group.some(text => normalizedBodyText.includes(normalize(text))));
                    }",
                    groups);
                if (found)
                {
                    return true;
                }
            }
            catch
            {
                // Some transient frames can disappear while the ERP screen is changing.
            }
        }

        return false;
    }

    private static async Task<bool> PageContainsLineResultRowAsync(
        IPage page,
        IReadOnlyCollection<IReadOnlyCollection<string>> groups,
        CancellationToken cancellationToken)
    {
        foreach (var frame in page.Frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var found = await frame.EvaluateAsync<bool>(
                    @"(groups) => {
                        const normalize = value => String(value || '').replace(/[,\s]/g, '');
                        const visible = element => {
                            const style = window.getComputedStyle(element);
                            const rect = element.getBoundingClientRect();
                            return style && style.visibility !== 'hidden' && style.display !== 'none' && rect.width > 0 && rect.height > 0;
                        };
                        const rowSelectors = [
                            'tr',
                            '[role=row]',
                            '.x-grid-row',
                            '.grid-row',
                            '.jqgrow',
                            '.ui-row-ltr'
                        ];
                        const rowText = row => {
                            const values = [row.innerText, row.textContent, row.getAttribute('title'), row.getAttribute('aria-label')];
                            row.querySelectorAll('input:not([type=hidden]), textarea, [title], [aria-label]').forEach(element => {
                                values.push(element.value, element.innerText, element.textContent, element.getAttribute('title'), element.getAttribute('aria-label'));
                            });
                            return normalize(values.filter(Boolean).join(' '));
                        };
                        const rows = Array.from(document.querySelectorAll(rowSelectors.join(',')))
                            .filter(visible)
                            .map(rowText)
                            .filter(text => text.length > 0);
                        if (rows.some(rowText => groups.every(group => group.some(text => rowText.includes(normalize(text)))))) {
                            return true;
                        }

                        const valueOf = element => [
                            element.value,
                            element.innerText,
                            element.textContent,
                            element.getAttribute('title'),
                            element.getAttribute('aria-label')
                        ].filter(Boolean).join(' ');
                        const hasVisibleValueChild = element => Array.from(element.children || [])
                            .some(child => visible(child) && normalize(valueOf(child)).length > 0);
                        const isCellLike = element => {
                            const tag = element.tagName.toLowerCase();
                            const role = element.getAttribute('role');
                            const rect = element.getBoundingClientRect();
                            if (tag === 'input' || tag === 'textarea' || tag === 'td' || tag === 'th') return true;
                            if (role === 'gridcell' || role === 'cell') return true;
                            return (tag === 'div' || tag === 'span') && !hasVisibleValueChild(element) && rect.height <= 80;
                        };
                        const cells = Array.from(document.querySelectorAll('td, th, div, span, input:not([type=hidden]), textarea, [role=gridcell], [role=cell]'))
                            .filter(visible)
                            .filter(isCellLike)
                            .map(element => ({ text: normalize(valueOf(element)), rect: element.getBoundingClientRect() }))
                            .filter(item => item.text.length > 0 && item.rect.width > 0 && item.rect.height > 0)
                            .sort((a, b) => a.rect.top - b.rect.top || a.rect.left - b.rect.left);
                        const visualRows = [];
                        for (const cell of cells) {
                            let row = visualRows.find(candidate => Math.abs(candidate.top - cell.rect.top) <= 12);
                            if (!row) {
                                row = { top: cell.rect.top, texts: [] };
                                visualRows.push(row);
                            }

                            row.texts.push(cell.text);
                        }

                        return visualRows
                            .map(row => row.texts.join(''))
                            .some(rowText => groups.every(group => group.some(text => rowText.includes(normalize(text)))));
                    }",
                    groups);
                if (found)
                {
                    return true;
                }
            }
            catch
            {
                // Some transient frames can disappear while the ERP screen is changing.
            }
        }

        return false;
    }

    private static async Task<bool> PageHasZeroNearAnyLabelAsync(
        IPage page,
        IReadOnlyCollection<string> labels,
        CancellationToken cancellationToken)
    {
        foreach (var frame in page.Frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var found = await frame.EvaluateAsync<bool>(
                    @"(labels) => {
                        const normalizeText = value => String(value || '').replace(/\s+/g, ' ').trim();
                        const normalizeNumber = value => String(value || '').replace(/[,\s]/g, '');
                        const visible = element => {
                            const style = window.getComputedStyle(element);
                            const rect = element.getBoundingClientRect();
                            return style && style.visibility !== 'hidden' && style.display !== 'none' && rect.width > 0 && rect.height > 0;
                        };
                        const valueOf = element => normalizeText(element.value || element.innerText || element.textContent || element.title);
                        const isZero = value => normalizeNumber(value) === '0';
                        const candidates = Array.from(document.querySelectorAll('label, th, td, span, div')).filter(element => visible(element));
                        for (const label of labels) {
                            for (const labelElement of candidates) {
                                if (!normalizeText(labelElement.innerText || labelElement.textContent).includes(label)) continue;

                                const row = labelElement.closest('tr');
                                if (row) {
                                    const rowValues = Array.from(row.querySelectorAll('input:not([type=hidden]), textarea, td, span, div'))
                                        .filter(element => element !== labelElement && visible(element))
                                        .map(valueOf)
                                        .filter(Boolean);
                                    if (rowValues.some(isZero)) return true;
                                }

                                const labelRect = labelElement.getBoundingClientRect();
                                const nearby = Array.from(document.querySelectorAll('input:not([type=hidden]), textarea, td, span, div'))
                                    .filter(element => element !== labelElement && visible(element))
                                    .map(element => ({ element, rect: element.getBoundingClientRect() }))
                                    .filter(item => Math.abs(item.rect.top - labelRect.top) < 70 && item.rect.left >= labelRect.left)
                                    .sort((a, b) => Math.abs(a.rect.left - labelRect.right) - Math.abs(b.rect.left - labelRect.right))
                                    .slice(0, 3)
                                    .map(item => valueOf(item.element))
                                    .filter(Boolean);
                                if (nearby.some(isZero)) return true;
                            }
                        }
                        return false;
                    }",
                    labels);
                if (found)
                {
                    return true;
                }
            }
            catch
            {
                // Some transient frames can disappear while the ERP screen is changing.
            }
        }

        return false;
    }

    private static async Task<bool> PageHasBusyIndicatorAsync(IPage page, CancellationToken cancellationToken)
    {
        foreach (var frame in page.Frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var found = await frame.EvaluateAsync<bool>(
                    @"() => {
                        const visible = element => {
                            const style = window.getComputedStyle(element);
                            const rect = element.getBoundingClientRect();
                            return style && style.visibility !== 'hidden' && style.display !== 'none' && rect.width > 0 && rect.height > 0;
                        };
                        const busyText = /처리중|조회중|검색중|로딩|잠시만|loading|please wait/i;
                        const busyClass = /(^|[-_\s])(loading|loadmask|spinner|progress|wait)([-_\s]|$)/i;
                        return Array.from(document.querySelectorAll('div, span, td, th, label, button, input'))
                            .filter(visible)
                            .some(element => {
                                const text = [element.innerText, element.textContent, element.value, element.title, element.getAttribute('aria-label')]
                                    .filter(Boolean)
                                    .join(' ');
                                const attrs = [element.id, element.className, element.getAttribute('role')]
                                    .filter(Boolean)
                                    .join(' ');
                                return element.getAttribute('aria-busy') === 'true' || busyText.test(text) || busyClass.test(attrs);
                            });
                    }");
                if (found)
                {
                    return true;
                }
            }
            catch
            {
                // Some transient frames can disappear while the ERP screen is changing.
            }
        }

        return false;
    }

    private static async Task RunInAnyFrameAsync(
        IPage page,
        Func<IFrame, Task<bool>> action,
        string description,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (await TryRunInAnyFrameAsync(page, action, timeout, cancellationToken))
        {
            return;
        }

        throw new TimeoutException($"{description} 대상을 찾지 못했습니다.");
    }

    private static async Task<bool> TryRunInAnyFrameAsync(
        IPage page,
        Func<IFrame, Task<bool>> action,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var frame in page.Frames)
            {
                try
                {
                    if (await action(frame))
                    {
                        return true;
                    }
                }
                catch
                {
                    // The ERP page changes frames frequently; try the next visible frame.
                }
            }

            await Task.Delay(350, cancellationToken);
        }

        return false;
    }

    private static async Task<bool> ClickTextInFrameAsync(
        IPage page,
        IFrame frame,
        string text,
        bool preferUpperArea = false,
        bool preferLowerArea = false)
    {
        var clickTarget = await frame.EvaluateAsync<ClickTargetResult>(
            @"({ text, preferUpperArea, preferLowerArea }) => {
                const normalize = value => (value || '').replace(/\s+/g, ' ').trim();
                const normalizeKey = value => normalize(value).replace(/[\s()[\]{}<>\/\\:_-]/g, '');
                const targetText = normalize(text);
                const targetKey = normalizeKey(text);
                const visible = element => {
                    const style = window.getComputedStyle(element);
                    const rect = element.getBoundingClientRect();
                    return style && style.visibility !== 'hidden' && style.display !== 'none' && rect.width > 0 && rect.height > 0;
                };
                const textOf = element => normalize(element.innerText || element.value || element.title || element.getAttribute('aria-label'));
                const matchesText = item => item.value === targetText
                    || item.value.includes(targetText)
                    || item.key.includes(targetKey)
                    || (targetKey.includes(item.key) && item.key.length >= Math.min(4, targetKey.length));
                const isActionElement = element => {
                    const tag = element.tagName.toLowerCase();
                    const role = normalize(element.getAttribute('role')).toLowerCase();
                    return tag === 'button'
                        || tag === 'a'
                        || tag === 'input'
                        || role === 'button'
                        || role === 'menuitem'
                        || typeof element.onclick === 'function';
                };
                const score = item => {
                    let value = 0;
                    if (item.value === targetText) value -= 1000;
                    if (item.key === targetKey) value -= 700;
                    if (isActionElement(item.element)) value -= 300;
                    if (preferUpperArea && item.rect.top > window.innerHeight * 0.45) value += 700;
                    if (preferLowerArea && item.rect.top < window.innerHeight * 0.45) value += 700;
                    value += Math.min(item.value.length, 300);
                    value += Math.min(item.rect.width * item.rect.height / 1000, 300);
                    return value;
                };
                const candidates = Array.from(document.querySelectorAll('button, a, input, div, span, td, li'));
                const matched = candidates
                    .filter(visible)
                    .map(element => {
                        const value = textOf(element);
                        return { element, value, key: normalizeKey(value), rect: element.getBoundingClientRect() };
                    })
                    .filter(matchesText)
                    .sort((a, b) => score(a) - score(b));
                if (matched.length === 0) return { Found: false, X: 0, Y: 0, CanMouseClick: false };
                const found = matched[0].element;
                found.scrollIntoView({ block: 'center', inline: 'center' });
                const rect = found.getBoundingClientRect();
                const x = rect.left + rect.width / 2;
                const y = rect.top + rect.height / 2;
                let pageX = x;
                let pageY = y;
                let canMouseClick = true;
                try {
                    let currentWindow = window;
                    while (currentWindow.frameElement) {
                        const frameRect = currentWindow.frameElement.getBoundingClientRect();
                        pageX += frameRect.left;
                        pageY += frameRect.top;
                        currentWindow = currentWindow.parent;
                    }
                } catch {
                    canMouseClick = window === window.top;
                }
                if (!canMouseClick) {
                    const target = document.elementFromPoint(x, y) || found;
                    const eventOptions = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };
                    target.dispatchEvent(new PointerEvent('pointerdown', eventOptions));
                    target.dispatchEvent(new MouseEvent('mousedown', eventOptions));
                    target.dispatchEvent(new PointerEvent('pointerup', eventOptions));
                    target.dispatchEvent(new MouseEvent('mouseup', eventOptions));
                    target.dispatchEvent(new MouseEvent('click', eventOptions));
                    if (target !== found) {
                        found.click();
                    }
                }
                return { Found: true, X: pageX, Y: pageY, CanMouseClick: canMouseClick };
            }",
            new { text, preferUpperArea, preferLowerArea });

        if (clickTarget is null || !clickTarget.Found)
        {
            return false;
        }

        if (clickTarget.CanMouseClick)
        {
            await page.Mouse.ClickAsync((float)clickTarget.X, (float)clickTarget.Y);
        }

        return true;
    }

    private sealed class ClickTargetResult
    {
        public bool Found { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public bool CanMouseClick { get; set; }
    }

    private static Task<bool> FillNearLabelInFrameAsync(
        IFrame frame,
        IReadOnlyCollection<string> labels,
        IReadOnlyCollection<string> values,
        bool preferLowerArea,
        bool preferWideControl)
    {
        return frame.EvaluateAsync<bool>(
            @"({ labels, values, preferLowerArea, preferWideControl }) => {
                const normalize = item => (item || '').replace(/\s+/g, ' ').trim();
                const normalizeKey = item => normalize(item).replace(/[\s()[\]{}<>\/\\:_-]/g, '');
                const targetKeys = Array.from(labels || []).map(normalizeKey).filter(key => key.length >= 2);
                const matchesLabel = element => {
                    const key = normalizeKey(element.innerText || element.textContent || element.value || element.title);
                    if (!key || key.length < 2) return false;
                    return targetKeys.some(targetKey =>
                        key.includes(targetKey) || (targetKey.includes(key) && key.length >= Math.min(4, targetKey.length)));
                };
                const visible = element => {
                    const style = window.getComputedStyle(element);
                    const rect = element.getBoundingClientRect();
                    return style && style.visibility !== 'hidden' && style.display !== 'none' && rect.width > 0 && rect.height > 0;
                };
                const isCredentialControl = control => {
                    const attrs = [
                        control.getAttribute('type'),
                        control.getAttribute('name'),
                        control.getAttribute('id'),
                        control.getAttribute('autocomplete'),
                        control.getAttribute('placeholder'),
                        control.getAttribute('aria-label')
                    ].map(normalizeKey).join(' ');
                    return attrs.includes('password')
                        || attrs.includes('passwd')
                        || attrs.includes('pwd')
                        || attrs.includes('currentpassword')
                        || attrs.includes('newpassword')
                        || attrs.includes('username')
                        || attrs.includes('userid')
                        || attrs.includes('loginid');
                };
                const chooseValue = control => {
                    const type = normalize(control.getAttribute('type')).toLowerCase();
                    const hint = normalize([
                        control.value,
                        control.placeholder,
                        control.getAttribute('data-format'),
                        control.getAttribute('format'),
                        control.getAttribute('maxlength')
                    ].filter(Boolean).join(' '));
                    const find = pattern => values.find(item => pattern.test(item)) || values[0];
                    if (type === 'date') return find(/^\d{4}-\d{2}-\d{2}$/);
                    if (type === 'number') return find(/^-?\d+(\.\d+)?$/);
                    if (hint.includes('.')) return find(/^\d{4}\.\d{2}\.\d{2}$/);
                    if (hint.includes('-')) return find(/^\d{4}-\d{2}-\d{2}$/);
                    if (control.maxLength === 8 || hint.includes('8')) return find(/^\d{8}$/);
                    return values[0];
                };
                const controls = () => Array.from(document.querySelectorAll('input:not([type=hidden]), textarea, [contenteditable=true]'))
                    .filter(element => visible(element) && !isCredentialControl(element));
                const setValue = control => {
                    const value = chooseValue(control);
                    control.scrollIntoView({ block: 'center', inline: 'center' });
                    control.focus();
                    if (control.isContentEditable) {
                        control.innerText = value;
                    } else {
                        control.value = value;
                    }
                    control.dispatchEvent(new Event('input', { bubbles: true }));
                    control.dispatchEvent(new Event('change', { bubbles: true }));
                    return true;
                };
                const scoreControl = (control, labelRect) => {
                    const rect = control.getBoundingClientRect();
                    let score = Math.abs(rect.top - labelRect.top) * 3 + Math.max(0, rect.left - labelRect.right);
                    if (rect.left < labelRect.left - 5) score += 1000;
                    if (preferLowerArea && rect.top < window.innerHeight * 0.45) score += 900;
                    if (preferWideControl) score -= Math.min(rect.width, 420);
                    return score;
                };
                const labels = Array.from(document.querySelectorAll('label, th, td, span, div')).filter(element => visible(element) && matchesLabel(element));
                for (const labelElement of labels) {
                    const row = labelElement.closest('tr');
                    const labelRect = labelElement.getBoundingClientRect();
                    if (row) {
                        const rowControls = Array.from(row.querySelectorAll('input:not([type=hidden]), textarea, [contenteditable=true]'))
                            .filter(element => visible(element) && !isCredentialControl(element));
                        const target = rowControls
                            .map(control => ({ control, score: scoreControl(control, labelRect) }))
                            .sort((a, b) => a.score - b.score)[0]?.control;
                        if (target) return setValue(target);
                    }

                    const nearby = controls()
                        .map(control => ({ control, rect: control.getBoundingClientRect() }))
                        .filter(item => Math.abs(item.rect.top - labelRect.top) < 90 && item.rect.left >= labelRect.left - 5)
                        .map(item => ({ control: item.control, score: scoreControl(item.control, labelRect) }))
                        .sort((a, b) => a.score - b.score)[0];
                    if (nearby) return setValue(nearby.control);
                }
                return false;
            }",
            new { labels, values, preferLowerArea, preferWideControl });
    }

    private static Task<bool> SelectNativeByLabelInFrameAsync(IFrame frame, IReadOnlyCollection<string> labels, string optionText)
    {
        return frame.EvaluateAsync<bool>(
            @"({ labels, optionText }) => {
                const normalize = item => (item || '').replace(/\s+/g, ' ').trim();
                const normalizeOption = item => normalize(item).replace(/[,\s()[\]{}<>\/\\:_-]/g, '');
                const normalizeKey = item => normalize(item).replace(/[\s()[\]{}<>\/\\:_-]/g, '');
                const targetKeys = Array.from(labels || []).map(normalizeKey).filter(key => key.length >= 2);
                const optionKey = normalizeOption(optionText);
                const matchesLabel = element => {
                    const key = normalizeKey(element.innerText || element.textContent || element.value || element.title);
                    if (!key || key.length < 2) return false;
                    return targetKeys.some(targetKey =>
                        key.includes(targetKey) || (targetKey.includes(key) && key.length >= Math.min(4, targetKey.length)));
                };
                const matchesOption = item => {
                    const textKey = normalizeOption(item.text);
                    const valueKey = normalizeOption(item.value);
                    return textKey.includes(optionKey)
                        || valueKey.includes(optionKey)
                        || (optionKey.includes(textKey) && textKey.length >= Math.min(4, optionKey.length))
                        || (optionKey.includes(valueKey) && valueKey.length >= Math.min(4, optionKey.length));
                };
                const visible = element => {
                    const style = window.getComputedStyle(element);
                    const rect = element.getBoundingClientRect();
                    return style && style.visibility !== 'hidden' && style.display !== 'none' && rect.width > 0 && rect.height > 0;
                };
                const labels = Array.from(document.querySelectorAll('label, th, td, span, div')).filter(element => visible(element) && matchesLabel(element));
                for (const labelElement of labels) {
                    const row = labelElement.closest('tr');
                    const scope = row || document;
                    const select = Array.from(scope.querySelectorAll('select')).filter(visible)[0];
                    if (select) {
                        const option = Array.from(select.options).find(matchesOption);
                        if (option) {
                            select.value = option.value;
                            select.dispatchEvent(new Event('input', { bubbles: true }));
                            select.dispatchEvent(new Event('change', { bubbles: true }));
                            return true;
                        }
                    }
                }
                return false;
            }",
            new { labels, optionText });
    }

    private static Task<bool> OpenDropdownByLabelInFrameAsync(IFrame frame, IReadOnlyCollection<string> labels)
    {
        return frame.EvaluateAsync<bool>(
            @"(labels) => {
                const normalize = item => (item || '').replace(/\s+/g, ' ').trim();
                const normalizeKey = item => normalize(item).replace(/[\s()[\]{}<>\/\\:_-]/g, '');
                const targetKeys = Array.from(labels || []).map(normalizeKey).filter(key => key.length >= 2);
                const matchesLabel = element => {
                    const key = normalizeKey(element.innerText || element.textContent || element.value || element.title);
                    if (!key || key.length < 2) return false;
                    return targetKeys.some(targetKey =>
                        key.includes(targetKey) || (targetKey.includes(key) && key.length >= Math.min(4, targetKey.length)));
                };
                const visible = element => {
                    const style = window.getComputedStyle(element);
                    const rect = element.getBoundingClientRect();
                    return style && style.visibility !== 'hidden' && style.display !== 'none' && rect.width > 0 && rect.height > 0;
                };
                const labels = Array.from(document.querySelectorAll('label, th, td, span, div')).filter(element => visible(element) && matchesLabel(element));
                for (const labelElement of labels) {
                    const labelRect = labelElement.getBoundingClientRect();
                    const controls = Array.from(document.querySelectorAll('select, input:not([type=hidden]), button, [role=combobox], [aria-haspopup]')).filter(visible);
                    const target = controls
                        .map(control => ({ control, rect: control.getBoundingClientRect() }))
                        .filter(item => Math.abs(item.rect.top - labelRect.top) < 80 && item.rect.left >= labelRect.left - 5)
                        .sort((a, b) => Math.abs(a.rect.left - labelRect.right) - Math.abs(b.rect.left - labelRect.right))[0];
                    if (target) {
                        target.control.scrollIntoView({ block: 'center', inline: 'center' });
                        target.control.focus();
                        const rect = target.control.getBoundingClientRect();
                        const x = rect.left + rect.width / 2;
                        const y = rect.top + rect.height / 2;
                        const clicked = document.elementFromPoint(x, y) || target.control;
                        const eventOptions = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };
                        clicked.dispatchEvent(new PointerEvent('pointerdown', eventOptions));
                        clicked.dispatchEvent(new MouseEvent('mousedown', eventOptions));
                        clicked.dispatchEvent(new PointerEvent('pointerup', eventOptions));
                        clicked.dispatchEvent(new MouseEvent('mouseup', eventOptions));
                        clicked.dispatchEvent(new MouseEvent('click', eventOptions));
                        return true;
                    }
                }
                return false;
            }",
            labels);
    }

    private static async Task SaveFailureArtifactsAsync(IPage page, AutomationInput input, IProgress<AutomationProgress> progress)
    {
        try
        {
            Directory.CreateDirectory(FailureDirectory);

            var artifactPaths = CreateFailureArtifactPaths(DateTime.Now);
            var screenshotPath = artifactPaths.ScreenshotPath;
            var htmlPath = artifactPaths.HtmlPath;

            var title = await GetPageTitleAsync(page);
            progress.Report(AutomationProgress.Warning($"실패 당시 페이지: {title}"));
            progress.Report(AutomationProgress.Warning($"실패 당시 URL: {page.Url}"));
            progress.Report(AutomationProgress.Warning($"실패 입력값: 거래일자 {input.TransactionDateText}, 수량 {input.QuantityText}, 단가 {input.UnitPriceText}"));
            progress.Report(AutomationProgress.Warning($"실패 입력값: 거래처코드 {input.ClientCode}, 계정코드 {input.CreditAccountCode}, 품목 {AutomationInput.ItemText}"));
            progress.Report(AutomationProgress.Warning($"실패 예상값: 공급가액 {input.SupplyAmountText}, 세액 {input.TaxAmountText}"));

            try
            {
                await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = screenshotPath,
                    FullPage = true
                });

                progress.Report(AutomationProgress.Warning($"실패 화면을 저장했습니다: {screenshotPath}"));
            }
            catch (Exception ex)
            {
                progress.Report(AutomationProgress.Warning($"실패 화면 저장 실패: {ex.Message}"));
            }

            try
            {
                await File.WriteAllTextAsync(htmlPath, await BuildFailureHtmlAsync(page, input));

                progress.Report(AutomationProgress.Warning($"실패 HTML을 저장했습니다: {htmlPath}"));
            }
            catch (Exception ex)
            {
                progress.Report(AutomationProgress.Warning($"실패 HTML 저장 실패: {ex.Message}"));
            }
        }
        catch (Exception ex)
        {
            progress.Report(AutomationProgress.Warning($"실패 자료 저장 준비 중 오류가 발생했습니다: {ex.Message}"));
        }
    }

    private static FailureArtifactPaths CreateFailureArtifactPaths(DateTime timestamp)
    {
        var baseName = $"erp_failure_{timestamp:yyyyMMdd_HHmmss_fff}";
        var suffix = 0;

        while (true)
        {
            var name = suffix == 0
                ? baseName
                : $"{baseName}_{suffix}";
            var screenshotPath = Path.Combine(FailureDirectory, $"{name}.png");
            var htmlPath = Path.Combine(FailureDirectory, $"{name}.html");

            if (!File.Exists(screenshotPath) && !File.Exists(htmlPath))
            {
                return new FailureArtifactPaths(screenshotPath, htmlPath);
            }

            suffix++;
        }
    }

    private sealed record FailureArtifactPaths(string ScreenshotPath, string HtmlPath);

    private static async Task<string> GetPageTitleAsync(IPage page)
    {
        try
        {
            var title = await page.TitleAsync();
            return string.IsNullOrWhiteSpace(title)
                ? "(제목 없음)"
                : title;
        }
        catch (Exception ex)
        {
            return $"(제목 확인 실패: {ex.Message})";
        }
    }

    private static async Task<string> BuildFailureHtmlAsync(IPage page, AutomationInput input)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html><head><meta charset=\"utf-8\"><title>ERP automation failure frames</title></head><body>");
        builder.AppendLine("<h1>ERP automation failure frames</h1>");
        builder.AppendLine($"<p>Captured at {WebUtility.HtmlEncode(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))}</p>");
        builder.AppendLine($"<p>Page title: {WebUtility.HtmlEncode(await GetPageTitleAsync(page))}</p>");
        builder.AppendLine($"<p>Page URL: {WebUtility.HtmlEncode(page.Url)}</p>");
        builder.AppendLine("<h2>Automation input</h2>");
        builder.AppendLine("<dl>");
        builder.AppendLine($"<dt>Transaction date</dt><dd>{WebUtility.HtmlEncode(input.TransactionDateText)}</dd>");
        builder.AppendLine($"<dt>Quantity</dt><dd>{WebUtility.HtmlEncode(input.QuantityText)}</dd>");
        builder.AppendLine($"<dt>Unit price</dt><dd>{WebUtility.HtmlEncode(input.UnitPriceText)}</dd>");
        builder.AppendLine($"<dt>Client code</dt><dd>{WebUtility.HtmlEncode(input.ClientCode)}</dd>");
        builder.AppendLine($"<dt>Credit account code</dt><dd>{WebUtility.HtmlEncode(input.CreditAccountCode)}</dd>");
        builder.AppendLine($"<dt>Item</dt><dd>{WebUtility.HtmlEncode(AutomationInput.ItemText)}</dd>");
        builder.AppendLine($"<dt>Expected supply amount</dt><dd>{WebUtility.HtmlEncode(input.SupplyAmountText)}</dd>");
        builder.AppendLine($"<dt>Expected tax amount</dt><dd>{WebUtility.HtmlEncode(input.TaxAmountText)}</dd>");
        builder.AppendLine("</dl>");

        var index = 1;
        foreach (var frame in page.Frames)
        {
            builder.AppendLine("<hr>");
            builder.AppendLine($"<h2>Frame {index}</h2>");
            builder.AppendLine($"<p>URL: {WebUtility.HtmlEncode(frame.Url)}</p>");

            try
            {
                builder.AppendLine("<pre>");
                builder.AppendLine(WebUtility.HtmlEncode(await frame.ContentAsync()));
                builder.AppendLine("</pre>");
            }
            catch (Exception ex)
            {
                builder.AppendLine($"<p>Frame content capture failed: {WebUtility.HtmlEncode(ex.Message)}</p>");
            }

            index++;
        }

        builder.AppendLine("</body></html>");
        return builder.ToString();
    }
}
