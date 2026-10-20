namespace PaymentGateway.Api.Models.Responses;

public class RejectedPaymentResponse
{
    public PaymentStatus Status { get; } = PaymentStatus.Rejected;
    public IReadOnlyList<string> Errors { get; init; } = [];
}
