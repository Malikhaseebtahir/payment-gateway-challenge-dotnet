namespace PaymentGateway.Api.Models;

/// <summary>
/// a payment that was sent to the acquiring bank, only the last four card digits are kept
/// the full card number and cvv are never stored.
/// </summary>
public record Payment(
    Guid Id,
    PaymentStatus Status,
    string CardNumberLastFour,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount);
