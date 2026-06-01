using AutoTest.ErpAutomation.Models;
using Microsoft.Playwright;
using System.IO;

namespace AutoTest.ErpAutomation.Services;

public sealed class ErpAutomationService
{
    private const string LoginUrl = "https://ibcenter.co.kr/erp/erp/erplogin/erplogin_dispatch.jsp";

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
                await ClickTextAsync(page, "로그인", stepTimeout, cancellationToken);
            });

            await StepAsync(progress, "[05/30] 로그인 성공 여부를 확인합니다.", async () =>
            {
                await WaitUntilAnyTextAsync(page, new[] { "회계관리", "로그아웃" }, stepTimeout, cancellationToken);
            });

            progress.Report(AutomationProgress.Info("[06/30] 로그인된 탭은 닫지 않고 유지합니다."));

            await StepAsync(progress, "[07/30] 회계관리 버튼을 클릭합니다.", () => ClickTextAsync(page, "회계관리", stepTimeout, cancellationToken));
            await StepAsync(progress, "[08/30] 거래전표 메뉴를 펼칩니다.", () => ClickTextAsync(page, "거래전표", stepTimeout, cancellationToken));
            await StepAsync(progress, "[09/30] 거래전표(매출등록) 메뉴를 펼칩니다.", () => ClickTextAsync(page, "거래전표(매출등록)", stepTimeout, cancellationToken));
            await StepAsync(progress, "[10/30] 원화 버튼을 클릭합니다.", () => ClickTextAsync(page, "원화", stepTimeout, cancellationToken));

            await StepAsync(progress, $"[11/30] 거래일자에 {input.TransactionDateText} 값을 입력합니다.", () =>
                FillNearLabelAsync(page, "거래일자", input.TransactionDateText, pressEnter: false, stepTimeout, cancellationToken));

            await StepAsync(progress, "[12/30] 차변에서 외상매출금 [1141]을 선택합니다.", async () =>
            {
                await SelectByLabelAsync(page, "차변", "외상매출금", stepTimeout, cancellationToken);
                await ClickTextAsync(page, "외상매출금", stepTimeout, cancellationToken);
            });

            await StepAsync(progress, "[13/30] 매출구분에서 서비스(사회및개인)업 폐차처리업을 선택합니다.", async () =>
            {
                await SelectByLabelAsync(page, "매출구분", "서비스(사회및개인)업 폐차처리업", stepTimeout, cancellationToken);
                await ClickTextAsync(page, "서비스(사회및개인)업 폐차처리업", stepTimeout, cancellationToken);
            });

            await StepAsync(progress, $"[14/30] 거래처코드/명에 {input.ClientCode} 값을 입력하고 Enter를 실행합니다.", () =>
                FillNearLabelAsync(page, "거래처코드/명", input.ClientCode, pressEnter: true, stepTimeout, cancellationToken));

            await StepAsync(progress, "[15/30] 담당부서에 20을 입력하고 Enter를 실행합니다.", () =>
                FillNearLabelAsync(page, "담당부서", "20", pressEnter: true, stepTimeout, cancellationToken));

            await StepAsync(progress, "[16/30] 전자(세금)계산서 발송구분에서 국세청HTS를 선택합니다.", async () =>
            {
                await SelectByLabelAsync(page, "전자(세금)계산서 발송구분", "국세청HTS", stepTimeout, cancellationToken);
                await ClickTextAsync(page, "국세청HTS", stepTimeout, cancellationToken);
            });

            await StepAsync(progress, $"[17/30] 품목코드/품목명(적요)에 {AutomationInput.ItemText}를 입력합니다.", () =>
                FillNearLabelAsync(page, "품목코드/품목명(적요)", AutomationInput.ItemText, pressEnter: false, stepTimeout, cancellationToken));

            await StepAsync(progress, $"[18/30] 수량에 {input.QuantityText} 값을 입력합니다.", () =>
                FillNearLabelAsync(page, "수량", input.QuantityText, pressEnter: false, stepTimeout, cancellationToken));

            await StepAsync(progress, $"[19/30] 단가에 {input.UnitPriceText} 값을 입력합니다.", () =>
                FillNearLabelAsync(page, "단가", input.UnitPriceText, pressEnter: false, stepTimeout, cancellationToken));

            await StepAsync(progress, "[20/30] 계산 버튼을 클릭합니다.", () => ClickTextAsync(page, "계산", stepTimeout, cancellationToken));

            await StepAsync(progress, "[21/30] 계산 결과가 정상 반영되었는지 확인합니다.", async () =>
            {
                var hasZeroAmount = await PageHasZeroNearAnyLabelAsync(page, new[] { "공급가액", "세액" }, cancellationToken);
                var ok = await PageContainsAllGroupsAsync(page, input.CalculationResultGroups, cancellationToken);
                if (hasZeroAmount || !ok)
                {
                    progress.Report(AutomationProgress.Warning("공급가액/세액이 0이거나 기대값을 찾지 못했습니다. 수량과 단가를 다시 입력한 뒤 계산을 재시도합니다."));
                    await FillNearLabelAsync(page, "수량", input.QuantityText, pressEnter: false, stepTimeout, cancellationToken);
                    await FillNearLabelAsync(page, "단가", input.UnitPriceText, pressEnter: false, stepTimeout, cancellationToken);
                    await ClickTextAsync(page, "계산", stepTimeout, cancellationToken);
                }

                await WaitUntilAllGroupsAsync(page, input.CalculationResultGroups, stepTimeout, cancellationToken);
            });

            await StepAsync(progress, $"[22/30] 계정코드(대변)에 {input.CreditAccountCode} 값을 입력하고 Enter를 실행합니다.", () =>
                FillNearLabelAsync(page, "계정코드(대변)", input.CreditAccountCode, pressEnter: true, stepTimeout, cancellationToken));

            await StepAsync(progress, "[23/30] 라인저장 전 입력값과 계산 결과를 확인한 뒤 라인저장(L) 버튼을 클릭합니다.", async () =>
            {
                await WaitUntilAllGroupsAsync(
                    page,
                    new IReadOnlyCollection<string>[]
                    {
                        new[] { AutomationInput.ItemText },
                        input.SupplyAmountCandidates,
                        input.TaxAmountCandidates
                    },
                    stepTimeout,
                    cancellationToken);
                await ClickTextAsync(page, "라인저장", stepTimeout, cancellationToken);
            });

            await StepAsync(progress, "[24/30] 라인 목록 반영 여부를 확인합니다.", async () =>
            {
                await WaitUntilAllGroupsAsync(page, input.LineResultGroups, stepTimeout, cancellationToken);
            });

            await StepAsync(progress, "[25/30] 거래전기[S] 버튼을 클릭합니다.", () => ClickTextAsync(page, "거래전기", stepTimeout, cancellationToken));
            await StepAsync(progress, "[26/30] 화면이 전기 완료 상태로 바뀌었는지 확인합니다.", () => WaitUntilAnyTextAsync(page, new[] { "전기 완료", "전기완료", "완료" }, stepTimeout, cancellationToken));
            await StepAsync(progress, "[27/30] 회계전표 동일자생성 버튼을 클릭합니다.", () => ClickTextAsync(page, "회계전표 동일자생성", stepTimeout, cancellationToken));
            await StepAsync(progress, "[28/30] 회계전표입력 화면으로 이동했는지 확인합니다.", () => WaitUntilAnyTextAsync(page, new[] { "회계전표입력", "회계전표 입력" }, stepTimeout, cancellationToken));
            await StepAsync(progress, "[29/30] 원장전기[P] 버튼을 클릭합니다.", () => ClickTextAsync(page, "원장전기", stepTimeout, cancellationToken));
            await StepAsync(progress, "[30/30] 원장전기 완료 상태가 표시되는지 확인합니다.", () => WaitUntilAnyTextAsync(page, new[] { "원장전기: 완료", "원장전기 완료", "완료" }, stepTimeout, cancellationToken));

            progress.Report(AutomationProgress.Info("ERP 매출등록 자동화 절차가 완료되었습니다."));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await SaveFailureArtifactsAsync(page, progress);
            throw;
        }
    }

    private static async Task<IPage> GetOrCreatePageAsync(IBrowserContext context)
    {
        var page = context.Pages.FirstOrDefault(p => !p.IsClosed)
            ?? await context.NewPageAsync();
        await page.BringToFrontAsync();
        return page;
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

    private static Task ClickTextAsync(IPage page, string text, TimeSpan timeout, CancellationToken cancellationToken)
    {
        return RunInAnyFrameAsync(page, frame => ClickTextInFrameAsync(frame, text), $"'{text}' 클릭", timeout, cancellationToken);
    }

    private static Task FillNearLabelAsync(IPage page, string label, string value, bool pressEnter, TimeSpan timeout, CancellationToken cancellationToken)
    {
        return RunInAnyFrameAsync(page, frame => FillNearLabelInFrameAsync(frame, label, value, pressEnter), $"'{label}' 입력", timeout, cancellationToken);
    }

    private static Task SelectByLabelAsync(IPage page, string label, string optionText, TimeSpan timeout, CancellationToken cancellationToken)
    {
        return RunInAnyFrameAsync(page, frame => SelectByLabelInFrameAsync(frame, label, optionText), $"'{label}' 선택", timeout, cancellationToken);
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

    private static async Task<bool> PageContainsAnyAsync(IPage page, IReadOnlyCollection<string> texts, CancellationToken cancellationToken)
    {
        foreach (var frame in page.Frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var found = await frame.EvaluateAsync<bool>(
                    @"(texts) => {
                        const bodyText = (document.body?.innerText || '').replace(/\s+/g, ' ');
                        return texts.some(text => bodyText.includes(text));
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
                        const bodyText = (document.body?.innerText || '').replace(/\s+/g, ' ');
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
                        return;
                    }
                }
                catch
                {
                    // The ERP page changes frames frequently; try the next visible frame.
                }
            }

            await Task.Delay(350, cancellationToken);
        }

        throw new TimeoutException($"{description} 대상을 찾지 못했습니다.");
    }

    private static Task<bool> ClickTextInFrameAsync(IFrame frame, string text)
    {
        return frame.EvaluateAsync<bool>(
            @"(text) => {
                const normalize = value => (value || '').replace(/\s+/g, ' ').trim();
                const visible = element => {
                    const style = window.getComputedStyle(element);
                    const rect = element.getBoundingClientRect();
                    return style && style.visibility !== 'hidden' && style.display !== 'none' && rect.width > 0 && rect.height > 0;
                };
                const candidates = Array.from(document.querySelectorAll('button, a, input, div, span, td, li'));
                const found = candidates.find(element => {
                    if (!visible(element)) return false;
                    const value = normalize(element.innerText || element.value || element.title || element.getAttribute('aria-label'));
                    return value === text || value.includes(text);
                });
                if (!found) return false;
                found.scrollIntoView({ block: 'center', inline: 'center' });
                const rect = found.getBoundingClientRect();
                const x = rect.left + rect.width / 2;
                const y = rect.top + rect.height / 2;
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
                return true;
            }",
            text);
    }

    private static Task<bool> FillNearLabelInFrameAsync(IFrame frame, string label, string value, bool pressEnter)
    {
        return frame.EvaluateAsync<bool>(
            @"({ label, value, pressEnter }) => {
                const normalize = item => (item || '').replace(/\s+/g, ' ').trim();
                const visible = element => {
                    const style = window.getComputedStyle(element);
                    const rect = element.getBoundingClientRect();
                    return style && style.visibility !== 'hidden' && style.display !== 'none' && rect.width > 0 && rect.height > 0;
                };
                const controls = () => Array.from(document.querySelectorAll('input:not([type=hidden]), textarea, [contenteditable=true]')).filter(visible);
                const setValue = control => {
                    control.scrollIntoView({ block: 'center', inline: 'center' });
                    control.focus();
                    if (control.isContentEditable) {
                        control.innerText = value;
                    } else {
                        control.value = value;
                    }
                    control.dispatchEvent(new Event('input', { bubbles: true }));
                    control.dispatchEvent(new Event('change', { bubbles: true }));
                    if (pressEnter) {
                        control.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', code: 'Enter', bubbles: true }));
                        control.dispatchEvent(new KeyboardEvent('keyup', { key: 'Enter', code: 'Enter', bubbles: true }));
                    }
                    return true;
                };
                const labels = Array.from(document.querySelectorAll('label, th, td, span, div')).filter(element => visible(element) && normalize(element.innerText).includes(label));
                for (const labelElement of labels) {
                    const row = labelElement.closest('tr');
                    if (row) {
                        const rowControls = Array.from(row.querySelectorAll('input:not([type=hidden]), textarea, [contenteditable=true]')).filter(visible);
                        const labelRect = labelElement.getBoundingClientRect();
                        const target = rowControls.find(control => control.getBoundingClientRect().left >= labelRect.left) || rowControls[0];
                        if (target) return setValue(target);
                    }

                    const labelRect = labelElement.getBoundingClientRect();
                    const nearby = controls()
                        .map(control => ({ control, rect: control.getBoundingClientRect() }))
                        .filter(item => Math.abs(item.rect.top - labelRect.top) < 70 && item.rect.left >= labelRect.left - 5)
                        .sort((a, b) => Math.abs(a.rect.left - labelRect.right) - Math.abs(b.rect.left - labelRect.right))[0];
                    if (nearby) return setValue(nearby.control);
                }
                return false;
            }",
            new { label, value, pressEnter });
    }

    private static Task<bool> SelectByLabelInFrameAsync(IFrame frame, string label, string optionText)
    {
        return frame.EvaluateAsync<bool>(
            @"({ label, optionText }) => {
                const normalize = item => (item || '').replace(/\s+/g, ' ').trim();
                const visible = element => {
                    const style = window.getComputedStyle(element);
                    const rect = element.getBoundingClientRect();
                    return style && style.visibility !== 'hidden' && style.display !== 'none' && rect.width > 0 && rect.height > 0;
                };
                const labels = Array.from(document.querySelectorAll('label, th, td, span, div')).filter(element => visible(element) && normalize(element.innerText).includes(label));
                for (const labelElement of labels) {
                    const row = labelElement.closest('tr');
                    const scope = row || document;
                    const select = Array.from(scope.querySelectorAll('select')).filter(visible)[0];
                    if (select) {
                        const option = Array.from(select.options).find(item => normalize(item.text).includes(optionText) || normalize(item.value).includes(optionText));
                        if (option) {
                            select.value = option.value;
                            select.dispatchEvent(new Event('change', { bubbles: true }));
                            return true;
                        }
                    }

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
            new { label, optionText });
    }

    private static async Task SaveFailureArtifactsAsync(IPage page, IProgress<AutomationProgress> progress)
    {
        try
        {
            var failureDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AutoTest.ErpAutomation",
                "Failures");
            Directory.CreateDirectory(failureDirectory);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var screenshotPath = Path.Combine(failureDirectory, $"erp_failure_{timestamp}.png");
            var htmlPath = Path.Combine(failureDirectory, $"erp_failure_{timestamp}.html");

            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });
            await File.WriteAllTextAsync(htmlPath, await page.ContentAsync());

            progress.Report(AutomationProgress.Warning($"실패 화면을 저장했습니다: {screenshotPath}"));
            progress.Report(AutomationProgress.Warning($"실패 HTML을 저장했습니다: {htmlPath}"));
        }
        catch (Exception ex)
        {
            progress.Report(AutomationProgress.Warning($"실패 자료 저장 중 오류가 발생했습니다: {ex.Message}"));
        }
    }
}
