using System.Net;
using System.Text;

using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Observability;
using PaymentGateway.Api.Tests.Fakes;

namespace PaymentGateway.Api.Tests;

public class BankClientTests
{
    private static readonly BankPaymentRequest Request = new("2222405343248877", "04/2027", "GBP", 100, "123");

    private class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private readonly TestMeterFactory _meterFactory = new();

    private BankClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://bank.test/") },
            new PaymentMetrics(_meterFactory),
            NullLogger<BankClient>.Instance);

    [Fact]
    public async Task SendsSnakeCaseRequestAndParsesResponse()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"authorized":true,"authorization_code":"abc"}""", Encoding.UTF8, "application/json")
        });

        var response = await CreateClient(handler).ProcessPaymentAsync(Request, CancellationToken.None);

        Assert.Equal(new BankPaymentResponse(true, "abc"), response);
        Assert.Equal(
            """{"card_number":"2222405343248877","expiry_date":"04/2027","currency":"GBP","amount":100,"cvv":"123"}""",
            handler.LastRequestBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task ThrowsBankUnavailableOnErrorStatus(HttpStatusCode statusCode)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(statusCode));

        await Assert.ThrowsAsync<BankUnavailableException>(
            () => CreateClient(handler).ProcessPaymentAsync(Request, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowsBankUnavailableWhenBankIsUnreachable()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<BankUnavailableException>(
            () => CreateClient(handler).ProcessPaymentAsync(Request, CancellationToken.None));
    }

    [Fact]
    public async Task RecordsBankDurationMetric()
    {
        using var collector = new MetricCollector<double>(_meterFactory, PaymentMetrics.MeterName, "bank.request.duration");
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAsync<BankUnavailableException>(
            () => CreateClient(handler).ProcessPaymentAsync(Request, CancellationToken.None));

        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal(false, measurement.Tags["succeeded"]);
    }
}
