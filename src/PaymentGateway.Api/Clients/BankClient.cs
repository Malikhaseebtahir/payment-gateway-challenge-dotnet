using System.Diagnostics;

using PaymentGateway.Api.Observability;

namespace PaymentGateway.Api.Clients;

public interface IBankClient
{
    Task<BankPaymentResponse> ProcessPaymentAsync(BankPaymentRequest request, CancellationToken cancellationToken);
}

public class BankClient : IBankClient
{
    private readonly HttpClient _httpClient;
    private readonly PaymentMetrics _metrics;
    private readonly ILogger<BankClient> _logger;

    public BankClient(HttpClient httpClient, PaymentMetrics metrics, ILogger<BankClient> logger)
    {
        _httpClient = httpClient;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<BankPaymentResponse> ProcessPaymentAsync(BankPaymentRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("payments", request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            Record(stopwatch, succeeded: false, statusCode: null);
            throw new BankUnavailableException("Could not reach the acquiring bank.", ex);
        }

        Record(stopwatch, response.IsSuccessStatusCode, (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
            throw new BankUnavailableException($"Acquiring bank responded with {(int)response.StatusCode}.");

        return await response.Content.ReadFromJsonAsync<BankPaymentResponse>(cancellationToken)
               ?? throw new BankUnavailableException("Acquiring bank returned an empty response.");
    }

    private void Record(Stopwatch stopwatch, bool succeeded, int? statusCode)
    {
        var elapsed = stopwatch.Elapsed.TotalMilliseconds;
        _metrics.BankRequestCompleted(elapsed, succeeded);
        _logger.LogInformation("Acquiring bank responded with {BankStatusCode} in {BankElapsedMs:F0} ms", statusCode, elapsed);
    }
}
