using PaymentGateway.Api.Clients;

namespace PaymentGateway.Api.Tests.Fakes;

/// <summary>mirrors simulator card-number rules for controller tests without Docker.</summary>
public class FakeBankClient : IBankClient
{
    public List<BankPaymentRequest> Requests { get; } = new();

    public Task<BankPaymentResponse> ProcessPaymentAsync(BankPaymentRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        return request.CardNumber[^1] switch
        {
            '0' => throw new BankUnavailableException("Acquiring bank responded with 503."),
            var digit when (digit - '0') % 2 == 1 => Task.FromResult(new BankPaymentResponse(true, Guid.NewGuid().ToString())),
            _ => Task.FromResult(new BankPaymentResponse(false, null))
        };
    }
}
