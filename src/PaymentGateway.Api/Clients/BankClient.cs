namespace PaymentGateway.Api.Clients;

public interface IBankClient
{
    Task<BankPaymentResponse> ProcessPaymentAsync(BankPaymentRequest request, CancellationToken cancellationToken);
}

public class BankClient : IBankClient
{
    private readonly HttpClient _httpClient;

    public BankClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BankPaymentResponse> ProcessPaymentAsync(BankPaymentRequest request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("payments", request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new BankUnavailableException("Could not reach the acquiring bank.", ex);
        }


        if (!response.IsSuccessStatusCode)
            throw new BankUnavailableException($"Acquiring bank responded with {(int)response.StatusCode}.");

        return await response.Content.ReadFromJsonAsync<BankPaymentResponse>(cancellationToken)
               ?? throw new BankUnavailableException("Acquiring bank returned an empty response.");
    }
}
