using System.Globalization;

namespace AutoTest.ErpAutomation.Models;

public sealed record AutomationInput(
    decimal Quantity,
    decimal UnitPrice,
    string ClientCode,
    string CreditAccountCode,
    DateOnly TransactionDate)
{
    public decimal SupplyAmount => Quantity * UnitPrice;

    public decimal TaxAmount => decimal.Round(SupplyAmount * 0.1m, 0, MidpointRounding.AwayFromZero);

    public string TransactionDateText => TransactionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public string TransactionDateDotText => TransactionDate.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);

    public string TransactionDateCompactText => TransactionDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    public IReadOnlyCollection<string> TransactionDateCandidates => Unique(
        TransactionDateText,
        TransactionDateDotText,
        TransactionDateCompactText);

    public string QuantityText => FormatNumber(Quantity);

    public string QuantityPlainText => FormatPlainNumber(Quantity);

    public IReadOnlyCollection<string> QuantityInputCandidates => Unique(QuantityText, QuantityPlainText);

    public string QuantitySearchText => NormalizeNumberText(QuantityText);

    public string UnitPriceText => FormatNumber(UnitPrice);

    public string UnitPricePlainText => FormatPlainNumber(UnitPrice);

    public IReadOnlyCollection<string> UnitPriceInputCandidates => Unique(UnitPriceText, UnitPricePlainText);

    public string UnitPriceSearchText => NormalizeNumberText(UnitPriceText);

    public string SupplyAmountText => FormatNumber(SupplyAmount);

    public string SupplyAmountPlainText => FormatPlainNumber(SupplyAmount);

    public string SupplyAmountSearchText => NormalizeNumberText(SupplyAmountText);

    public string TaxAmountText => FormatNumber(TaxAmount);

    public string TaxAmountPlainText => FormatPlainNumber(TaxAmount);

    public string TaxAmountSearchText => NormalizeNumberText(TaxAmountText);

    public IReadOnlyCollection<string> QuantityCandidates => Unique(QuantitySearchText);

    public IReadOnlyCollection<string> UnitPriceCandidates => Unique(UnitPriceSearchText);

    public IReadOnlyCollection<string> SupplyAmountCandidates => Unique(SupplyAmountSearchText);

    public IReadOnlyCollection<string> TaxAmountCandidates => Unique(TaxAmountSearchText);

    public IReadOnlyCollection<string> CalculationResultCandidates => Unique(
        SupplyAmountSearchText,
        TaxAmountSearchText);

    public IReadOnlyCollection<IReadOnlyCollection<string>> CalculationResultGroups => new[]
    {
        SupplyAmountCandidates,
        TaxAmountCandidates
    };

    public IReadOnlyCollection<IReadOnlyCollection<string>> BeforeLineSaveGroups => new[]
    {
        new[] { ItemText },
        QuantityCandidates,
        UnitPriceCandidates,
        SupplyAmountCandidates,
        TaxAmountCandidates
    };

    public IReadOnlyCollection<string> LineResultCandidates => Unique(
        ItemText,
        QuantitySearchText,
        UnitPriceSearchText,
        SupplyAmountSearchText,
        TaxAmountSearchText);

    public IReadOnlyCollection<IReadOnlyCollection<string>> LineResultGroups => new[]
    {
        new[] { ItemText },
        QuantityCandidates,
        UnitPriceCandidates,
        SupplyAmountCandidates,
        TaxAmountCandidates
    };

    public const string ItemText = "차피 압축";

    public static bool TryParse(
        string quantityText,
        string unitPriceText,
        string clientCode,
        string creditAccountCode,
        DateTime? transactionDate,
        out AutomationInput? input,
        out string error)
    {
        input = null;
        error = string.Empty;

        if (!TryParseDecimal(quantityText, out var quantity) || quantity <= 0)
        {
            error = "수량은 0보다 큰 숫자로 입력해야 합니다.";
            return false;
        }

        if (!TryParseDecimal(unitPriceText, out var unitPrice) || unitPrice <= 0)
        {
            error = "단가는 0보다 큰 숫자로 입력해야 합니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(clientCode))
        {
            error = "거래처코드를 입력해야 합니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(creditAccountCode))
        {
            error = "계정코드를 입력해야 합니다.";
            return false;
        }

        input = new AutomationInput(
            quantity,
            unitPrice,
            clientCode.Trim(),
            creditAccountCode.Trim(),
            DateOnly.FromDateTime(transactionDate ?? DateTime.Today));
        return true;
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        return decimal.TryParse(
            text,
            NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.CurrentCulture,
            out value)
            || decimal.TryParse(
                text,
                NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value);
    }

    private static string FormatNumber(decimal value)
    {
        return value % 1 == 0
            ? value.ToString("#,0", CultureInfo.InvariantCulture)
            : value.ToString("#,0.####", CultureInfo.InvariantCulture);
    }

    private static string FormatPlainNumber(decimal value)
    {
        return value % 1 == 0
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private static IReadOnlyCollection<string> Unique(params string[] values)
    {
        return values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToArray();
    }

    private static string NormalizeNumberText(string value)
    {
        return value.Replace(",", string.Empty).Replace(" ", string.Empty);
    }
}
