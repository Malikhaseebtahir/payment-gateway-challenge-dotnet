using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Validation;

public class PaymentRequestValidator
{
    public static readonly IReadOnlySet<string> SupportedCurrencies = new HashSet<string> { "GBP", "USD", "EUR" };

    private readonly TimeProvider _timeProvider;

    public PaymentRequestValidator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public IReadOnlyList<string> Validate(PostPaymentRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(request.CardNumber))
            errors.Add("Card number is required.");
        else if (request.CardNumber.Length is < 14 or > 19)
            errors.Add("Card number must be between 14 and 19 characters long.");
        else if (!IsNumeric(request.CardNumber))
            errors.Add("Card number must only contain numeric characters.");

        if (request.ExpiryMonth is < 1 or > 12)
            errors.Add("Expiry month must be between 1 and 12.");
        else if (!IsInFuture(request.ExpiryMonth, request.ExpiryYear))
            errors.Add("Expiry date must be in the future.");

        if (string.IsNullOrEmpty(request.Currency))
            errors.Add("Currency is required.");
        else if (request.Currency.Length != 3)
            errors.Add("Currency must be 3 characters.");
        else if (!SupportedCurrencies.Contains(request.Currency))
            errors.Add($"Currency must be one of: {string.Join(", ", SupportedCurrencies)}.");

        if (request.Amount <= 0)
            errors.Add("Amount must be a positive integer in the minor currency unit.");

        if (string.IsNullOrEmpty(request.Cvv))
            errors.Add("CVV is required.");
        else if (request.Cvv.Length is < 3 or > 4)
            errors.Add("CVV must be 3-4 characters long.");
        else if (!IsNumeric(request.Cvv))
            errors.Add("CVV must only contain numeric characters.");

        return errors;
    }

    private static bool IsNumeric(string value) => value.All(char.IsAsciiDigit);

    // a card is valid until the end of its expiry month so the current month still counts as the future.
    private bool IsInFuture(int month, int year)
    {
        var now = _timeProvider.GetUtcNow();
        return year > now.Year || (year == now.Year && month >= now.Month);
    }
}
