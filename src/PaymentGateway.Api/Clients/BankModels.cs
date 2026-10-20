using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Clients;

public record BankPaymentRequest(
    [property: JsonPropertyName("card_number")] string CardNumber,
    [property: JsonPropertyName("expiry_date")] string ExpiryDate,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("amount")] int Amount,
    [property: JsonPropertyName("cvv")] string Cvv);

public record BankPaymentResponse(
    [property: JsonPropertyName("authorized")] bool Authorized,
    [property: JsonPropertyName("authorization_code")] string? AuthorizationCode);

/// <summary>Thrown when the acquiring bank could not give us an authorization decision.</summary>
public class BankUnavailableException : Exception
{
    public BankUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
