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
        var dialogHandler = AttachDialogHandler(page, progress);

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
                if (await PageContainsAnyAsync(page, new[] { "회계관리", "로그아웃" }, cancellationToken))
                {
                    progress.Report(AutomationProgress.Info("이미 로그인된 화면으로 판단되어 로그인 버튼 클릭을 생략합니다."));
                    return;
                }

                await ClickTextAsync(page, "로그인", stepTimeout, cancellationToken);
            });

            await StepAsync(progress, "[05/30] 로그인 성공 여부를 확인합니다.", async () =>
            {
                await WaitUntilLoginSuccessAsync(page, stepTimeout, cancellationToken);
            });

            progress.Report(AutomationProgress.Info("[06/30] 로그인된 탭은 닫지 않고 유지합니다."));

            await StepAsync(progress, "[07/30] 회계관리 버튼을 클릭합니다.", () => ClickTextAsync(page, "회계관리", stepTimeout, cancellationToken));
            await StepAsync(progress, "[08/30] 거래전표 메뉴를 펼칩니다.", () => ClickTextAsync(page, "거래전표", stepTimeout, cancellationToken));
            await StepAsync(progress, "[09/30] 거래전표(매출등록) 메뉴를 펼칩니다.", () => ClickTextAsync(page, "거래전표(매출등록)", stepTimeout, cancellationToken));
            await StepAsync(progress, "[10/30] 원화 버튼을 클릭합니다.", () => ClickTextAsync(page, "원화", stepTimeout, cancellationToken));

            await StepAsync(progress, $"[11/30] 거래일자에 오늘 날짜({input.TransactionDateText})를 입력합니다.", () =>
                FillNearLabelAsync(page, "거래일자", input.TransactionDateCandidates, pressEnter: false, stepTimeout, cancellationToken));

            await StepAsync(progress, "[12/30] 차변에서 외상매출금 [1141]을 선택합니다.", async () =>
            {
                await SelectByLabelAsync(page, "차변", new[] { "외상매출금 [1141]", "외상매출금" }, stepTimeout, cancellationToken);
            });

            await StepAsync(progress, "[13/30] 매출구분에서 서비스(사회및개인)업 폐차처리업을 선택합니다.", async () =>
            {
                await SelectByLabelAsync(page, "매출구분", "서비스(사회및개인)업 폐차처리업", stepTimeout, cancellationToken);
            });

            await StepAsync(progress, $"[14/30] 거래처코드/명에 {input.ClientCode} 값을 입력하고 Enter를 실행합니다.", () =>
                FillNearLabelAsync(page, "거래처코드/명", input.ClientCode, pressEnter: true, stepTimeout, cancellationToken));

            await StepAsync(progress, "[15/30] 담당부서에 20을 입력하고 Enter를 실행합니다.", () =>
                FillNearLabelAsync(page, "담당부서", "20", pressEnter: true, stepTimeout, cancellationToken));

            await StepAsync(progress, "[16/30] 전자(세금)계산서 발송구분에서 국세청HTS를 선택합니다.", async () =>
            {
                await SelectByLabelAsync(page, "전자(세금)계산서 발송구분", "국세청HTS", stepTimeout, cancellationToken);
            });

            await StepAsync(progress, $"[17/30] 품목코드/품목명(적요)에 {AutomationInput.ItemText}를 입력합니다.", () =>
                FillNearLabelAsync(page, "품목코드/품목명(적요)", AutomationInput.ItemText, pressEnter: false, stepTimeout, cancellationToken, preferLowerArea: true, preferWideControl: true));

            await StepAsync(progress, $"[18/30] 수량에 {input.QuantityText} 값을 입력합니다.", () =>
                FillNearLabelAsync(page, "수량", input.QuantityText, pressEnter: false, stepTimeout, cancellationToken, preferLowerArea: true));

            await StepAsync(progress, $"[19/30] 단가에 {input.UnitPriceText} 값을 입력합니다.", () =>
                FillNearLabelAsync(page, "단가", input.UnitPriceText, pressEnter: false, stepTimeout, cancellationToken, preferLowerArea: true));

            await StepAsync(progress, "[20/30] 계산 버튼을 클릭합니다.", () => ClickTextAsync(page, "계산", stepTimeout, cancellationToken));

            await StepAsync(progress, "[21/30] 계산 결과가 정상 반영되었는지 확인합니다.", async () =>
            {
                var hasZeroAmount = await PageHasZeroNearAnyLabelAsync(page, new[] { "공급가액", "세액" }, cancellationToken);
                var ok = await PageContainsAllGroupsAsync(page, input.CalculationResultGroups, cancellationToken);
                if (hasZeroAmount || !ok)
                {
                    progress.Report(AutomationProgress.Warning("공급가액/세액이 0이거나 기대값을 찾지 못했습니다. 수량과 단가를 다시 입력한 뒤 계산을 재시도합니다."));
                    await FillNearLabelAsync(page, "수량", input.QuantityText, pressEnter: false, stepTimeout, cancellationToken, preferLowerArea: true);
                    await FillNearLabelAsync(page, "단가", input.UnitPriceText, pressEnter: false, stepTimeout, cancellationToken, preferLowerArea: true);
                    await ClickTextAsync(page, "계산", stepTimeout, cancellationToken);
                }

                await WaitUntilAllGroupsAsync(page, input.CalculationResultGroups, stepTimeout, cancellationToken);
            });

            await StepAsync(progress, $"[22/30] 계정코드(대변)에 {input.CreditAccountCode} 값을 입력하고 Enter를 실행합니다.", () =>
                FillNearLabelAsync(page, "계정코드(대변)", input.CreditAccountCode, pressEnter: true, stepTimeout, cancellationToken, preferLowerArea: true));

            await StepAsync(progress, "[23/30] 라인저장 전 입력값과 계산 결과를 확인한 뒤 라인저장(L) 버튼을 클릭합니다.", async () =>
            {
                await WaitUntilAllGroupsAsync(page, input.BeforeLineSaveGroups, stepTimeout, cancellationToken);
                await ClickTextAsync(page, "라인저장", stepTimeout, cancellationToken, preferLowerArea: true);
            });

            await StepAsync(progress, "[24/30] 라인 목록 반영 여부를 확인합니다.", async () =>
            {
                await WaitUntilLineResultRowAsync(page, input.LineResultGroups, stepTimeout, cancellationToken);
            });

            await StepAsync(progress, "[25/30] 거래전기[S] 버튼을 클릭합니다.", () => ClickTextAsync(page, "거래전기", stepTimeout, cancellationToken, preferUpperArea: true));
            await StepAsync(progress, "[26/30] 화면이 전기 완료 상태로 바뀌었는지 확인합니다.", () => WaitUntilAnyTextAsync(page, new[] { "전기 완료", "전기완료", "거래전기 완료", "거래전기: 완료" }, stepTimeout, cancellationToken));
            await StepAsync(progress, "[27/30] 회계전표 동일자생성 버튼을 클릭합니다.", () => ClickTextAsync(page, "회계전표 동일자생성", stepTimeout, cancellationToken, preferUpperArea: true));
            await StepAsync(progress, "[28/30] 회계전표입력 화면으로 이동했는지 확인합니다.", () => WaitUntilAnyTextAsync(page, new[] { "회계전표입력", "회계전표 입력" }, stepTimeout, cancellationToken));
            await StepAsync(progress, "[29/30] 원장전기[P] 버튼을 클릭합니다.", () => ClickTextAsync(page, "원장전기", stepTimeout, cancellationToken, preferUpperArea: true));
            await StepAsync(progress, "[30/30] 원장전기 완료 상태가 표시되는지 확인합니다.", () => WaitUntilAnyTextAsync(page, new[] { "원장전기: 완료", "원장전기 완료" }, stepTimeout, cancellationToken));

            progress.Report(AutomationProgress.Info("ERP 매출등록 자동화 절차가 완료되었습니다."));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await SaveFailureArtifactsAsync(page, progress);
            throw;
        }
        finally
        {
            page.Dialog -= dialogHandler;
        }
    }

    private static async Task<IPage> GetOrCreatePageAsync(IBrowserContext context)
    {
        var page = context.Pages.FirstOrDefault(p => !p.IsClosed)
            ?? await context.NewPageAsync();
        await page.BringToFrontAsync();
        return page;
    }

    private static EventHandler<IDialog> AttachDialogHandler(IPage page, IProgress<AutomationProgress> progress)
    {
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
        return Handler;
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

    private static Task ClickTextAsync(
        IPage page,
        string text,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        bool preferUpperArea = false,
        bool preferLowerArea = false)
    {
        return RunInAnyFrameAsync(
            page,
            frame => ClickTextInFrameAsync(page, frame, text, preferUpperArea, preferLowerArea),
            $"'{text}' 클릭",
            timeout,
            cancellationToken);
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
        await FillNearLabelAsync(page, label, new[] { value }, pressEnter, timeout, cancellationToken, preferLowerArea, preferWideControl);
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
        await RunInAnyFrameAsync(
            page,
            frame => FillNearLabelInFrameAsync(frame, label, values, preferLowerArea, preferWideControl),
            $"'{label}' 입력",
            timeout,
            cancellationToken);

        if (pressEnter)
        {
            await page.Keyboard.PressAsync("Enter");
            await Task.Delay(200, cancellationToken);
        }
    }

    private static Task SelectByLabelAsync(IPage page, string label, string optionText, TimeSpan timeout, CancellationToken cancellationToken)
    {
        return SelectByLabelAsync(page, label, new[] { optionText }, timeout, cancellationToken);
    }

    private static async Task SelectByLabelAsync(
        IPage page,
        string label,
        IReadOnlyCollection<string> optionTexts,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var quickTimeout = TimeSpan.FromMilliseconds(Math.Min(timeout.TotalMilliseconds, 2500));
        foreach (var optionText in optionTexts)
        {
            if (await TryRunInAnyFrameAsync(
                page,
                frame => SelectNativeByLabelInFrameAsync(frame, label, optionText),
                quickTimeout,
                cancellationToken))
            {
                return;
            }
        }

        await RunInAnyFrameAsync(page, frame => OpenDropdownByLabelInFrameAsync(frame, label), $"'{label}' 드롭다운 열기", timeout, cancellationToken);

        foreach (var optionText in optionTexts)
        {
            if (await TryRunInAnyFrameAsync(
                page,
                frame => ClickTextInFrameAsync(page, frame, optionText),
                quickTimeout,
                cancellationToken))
            {
                return;
            }
        }

        throw new TimeoutException($"'{label}' 드롭다운에서 다음 옵션을 찾지 못했습니다: {string.Join(", ", optionTexts)}");
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

    private static async Task WaitUntilLoginSuccessAsync(IPage page, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        var successTexts = new[] { "회계관리", "로그아웃" };
        var passwordChangeTexts = new[] { "비밀번호 변경", "비밀번호를 변경", "새 비밀번호", "현재 비밀번호", "비밀번호 재설정", "password change" };
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await PageContainsAnyAsync(page, successTexts, cancellationToken))
            {
                return;
            }

            if (await PageContainsAnyAsync(page, passwordChangeTexts, cancellationToken))
            {
                throw new InvalidOperationException("비밀번호 입력 또는 변경 화면이 감지되어 자동화를 중단합니다. 자동화는 아이디/비밀번호를 입력하거나 변경하지 않습니다.");
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
                        return rows.some(rowText => groups.every(group => group.some(text => rowText.includes(normalize(text)))));
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

    private static async Task<bool> ClickTextInFrameAsync(IPage page, IFrame frame, string text, bool preferUpperArea, bool preferLowerArea)
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
                const matchesText = item => item.value === targetText || item.value.includes(targetText) || item.key.includes(targetKey);
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
        string label,
        IReadOnlyCollection<string> values,
        bool preferLowerArea,
        bool preferWideControl)
    {
        return frame.EvaluateAsync<bool>(
            @"({ label, values, preferLowerArea, preferWideControl }) => {
                const normalize = item => (item || '').replace(/\s+/g, ' ').trim();
                const normalizeKey = item => normalize(item).replace(/[\s()[\]{}<>\/\\:_-]/g, '');
                const targetKey = normalizeKey(label);
                const matchesLabel = element => {
                    const key = normalizeKey(element.innerText || element.textContent || element.value || element.title);
                    if (!key || key.length < 2) return false;
                    return key.includes(targetKey) || (targetKey.includes(key) && key.length >= Math.min(4, targetKey.length));
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
            new { label, values, preferLowerArea, preferWideControl });
    }

    private static Task<bool> SelectNativeByLabelInFrameAsync(IFrame frame, string label, string optionText)
    {
        return frame.EvaluateAsync<bool>(
            @"({ label, optionText }) => {
                const normalize = item => (item || '').replace(/\s+/g, ' ').trim();
                const normalizeOption = item => normalize(item).replace(/[,\s()[\]{}<>\/\\:_-]/g, '');
                const normalizeKey = item => normalize(item).replace(/[\s()[\]{}<>\/\\:_-]/g, '');
                const targetKey = normalizeKey(label);
                const optionKey = normalizeOption(optionText);
                const matchesLabel = element => {
                    const key = normalizeKey(element.innerText || element.textContent || element.value || element.title);
                    if (!key || key.length < 2) return false;
                    return key.includes(targetKey) || (targetKey.includes(key) && key.length >= Math.min(4, targetKey.length));
                };
                const matchesOption = item => {
                    const textKey = normalizeOption(item.text);
                    const valueKey = normalizeOption(item.value);
                    return textKey.includes(optionKey) || valueKey.includes(optionKey);
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
            new { label, optionText });
    }

    private static Task<bool> OpenDropdownByLabelInFrameAsync(IFrame frame, string label)
    {
        return frame.EvaluateAsync<bool>(
            @"(label) => {
                const normalize = item => (item || '').replace(/\s+/g, ' ').trim();
                const normalizeKey = item => normalize(item).replace(/[\s()[\]{}<>\/\\:_-]/g, '');
                const targetKey = normalizeKey(label);
                const matchesLabel = element => {
                    const key = normalizeKey(element.innerText || element.textContent || element.value || element.title);
                    if (!key || key.length < 2) return false;
                    return key.includes(targetKey) || (targetKey.includes(key) && key.length >= Math.min(4, targetKey.length));
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
            label);
    }

    private static async Task SaveFailureArtifactsAsync(IPage page, IProgress<AutomationProgress> progress)
    {
        try
        {
            Directory.CreateDirectory(FailureDirectory);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var screenshotPath = Path.Combine(FailureDirectory, $"erp_failure_{timestamp}.png");
            var htmlPath = Path.Combine(FailureDirectory, $"erp_failure_{timestamp}.html");

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
                await File.WriteAllTextAsync(htmlPath, await BuildFailureHtmlAsync(page));

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

    private static async Task<string> BuildFailureHtmlAsync(IPage page)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html><head><meta charset=\"utf-8\"><title>ERP automation failure frames</title></head><body>");
        builder.AppendLine("<h1>ERP automation failure frames</h1>");
        builder.AppendLine($"<p>Captured at {WebUtility.HtmlEncode(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))}</p>");

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
