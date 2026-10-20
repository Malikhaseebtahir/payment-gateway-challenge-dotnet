namespace PaymentGateway.Api.Models.Requests;

public class PostPaymentRequest
{
    /// <summary>The full card number 14-19 digits.</summary>
    public string? CardNumber { get; set; }

    /// <summary>Card expiry month 1-12.</summary>
    public int ExpiryMonth { get; set; }

    /// <summary>Card expiry year.</summary>
    public int ExpiryYear { get; set; }

    /// <summary>ISO 4217 currency code. supported: GBP, USD, EUR.</summary>
    public string? Currency { get; set; }

    /// <summary>Amount in the minor currency unit, e.g. 1050 for £10.50.</summary>
    public int Amount { get; set; }

    /// <summary>Card security code 3-4 digits.</summary>
    public string? Cvv { get; set; }
}