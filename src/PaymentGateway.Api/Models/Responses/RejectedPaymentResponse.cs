namespace PaymentGateway.Api.Models.Responses;

public class RejectedPaymentResponse
{
    /// <summary>always rejected: the bank was not contacted and no payment was created.</summary>
    public PaymentStatus Status { get; } = PaymentStatus.Rejected;

    /// <summary>every problem found with the request.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}
