using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Tests.Fakes;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PaymentsRepository _repository = new();
    private readonly FakeBankClient _bankClient = new();
    private readonly HttpClient _client;

    public PaymentsControllerTests()
    {
        _client = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.AddSingleton(_repository);
                services.AddSingleton<IBankClient>(_bankClient);
            }))
            .CreateClient();
    }

    private static PostPaymentRequest ValidRequest(string cardNumber = "2222405343248877") => new()
    {
        CardNumber = cardNumber,
        ExpiryMonth = 4,
        ExpiryYear = DateTime.UtcNow.Year + 1,
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

    [Theory]
    [InlineData("2222405343248877", PaymentStatus.Authorized)]
    [InlineData("2222405343248112", PaymentStatus.Declined)]
    public async Task ProcessesPaymentAndReturnsBankOutcome(string cardNumber, PaymentStatus expectedStatus)
    {
        var request = ValidRequest(cardNumber);

        var response = await _client.PostAsJsonAsync("/api/Payments", request);
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(payment);
        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal(expectedStatus, payment.Status);
        Assert.Equal(cardNumber[^4..], payment.CardNumberLastFour);
        Assert.Equal(request.ExpiryMonth, payment.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, payment.ExpiryYear);
        Assert.Equal(request.Currency, payment.Currency);
        Assert.Equal(request.Amount, payment.Amount);
        Assert.Equal($"/api/Payments/{payment.Id}", response.Headers.Location?.AbsolutePath);
        Assert.NotNull(_repository.Get(payment.Id));
    }

    [Fact]
    public async Task SendsCorrectlyFormattedRequestToBank()
    {
        var request = ValidRequest();

        await _client.PostAsJsonAsync("/api/Payments", request);

        var bankRequest = Assert.Single(_bankClient.Requests);
        Assert.Equal(new BankPaymentRequest(request.CardNumber!, $"04/{request.ExpiryYear}", "GBP", 100, "123"), bankRequest);
    }

    [Fact]
    public async Task ResponseNeverContainsFullCardNumberOrCvv()
    {
        var request = ValidRequest();

        var response = await _client.PostAsJsonAsync("/api/Payments", request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(request.CardNumber!, body);
        Assert.DoesNotContain("cvv", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RejectsInvalidPaymentWithoutCallingBank()
    {
        var request = ValidRequest();
        request.CardNumber = "123";
        request.Cvv = "12a";

        var response = await _client.PostAsJsonAsync("/api/Payments", request);
        var rejection = await response.Content.ReadFromJsonAsync<RejectedPaymentResponse>(JsonOptions);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"status\":\"Rejected\"", body);
        Assert.Equal(2, rejection!.Errors.Count);
        Assert.Empty(_bankClient.Requests);
    }

    [Fact]
    public async Task RejectsMalformedBody()
    {
        var content = new StringContent("""{"cardNumber":"2222405343248877","amount":"lots"}""", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/Payments", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"status\":\"Rejected\"", body);
        Assert.Contains("Field 'amount' has an invalid value.", body);
        Assert.DoesNotContain("System.", body);
        Assert.Empty(_bankClient.Requests);
    }

    [Fact]
    public async Task RejectsBodyThatIsNotJson()
    {
        var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/Payments", content);
        var rejection = await response.Content.ReadFromJsonAsync<RejectedPaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotEmpty(rejection!.Errors);
        Assert.Empty(_bankClient.Requests);
    }

    [Fact]
    public async Task Returns502AndDoesNotStorePaymentWhenBankIsUnavailable()
    {
        var response = await _client.PostAsJsonAsync("/api/Payments", ValidRequest("2222405343248870"));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Single(_bankClient.Requests);
    }

    [Fact]
    public async Task RetrievesAPaymentSuccessfully()
    {
        var payment = new Payment(Guid.NewGuid(), PaymentStatus.Authorized, "0123", 4, 2030, "USD", 1050);
        _repository.Add(payment);

        var response = await _client.GetAsync($"/api/Payments/{payment.Id}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<GetPaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(payment.Id, paymentResponse.Id);
        Assert.Equal(PaymentStatus.Authorized, paymentResponse.Status);
        Assert.Equal("0123", paymentResponse.CardNumberLastFour);
        Assert.Equal(4, paymentResponse.ExpiryMonth);
        Assert.Equal(2030, paymentResponse.ExpiryYear);
        Assert.Equal("USD", paymentResponse.Currency);
        Assert.Equal(1050, paymentResponse.Amount);
    }

    [Fact]
    public async Task ProcessedPaymentCanBeRetrieved()
    {
        var postResponse = await _client.PostAsJsonAsync("/api/Payments", ValidRequest());
        var created = await postResponse.Content.ReadFromJsonAsync<PostPaymentResponse>(JsonOptions);

        var getResponse = await _client.GetAsync(postResponse.Headers.Location);
        var retrieved = await getResponse.Content.ReadFromJsonAsync<GetPaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(created!.Id, retrieved!.Id);
        Assert.Equal(created.Status, retrieved.Status);
    }

    [Fact]
    public async Task Returns404IfPaymentNotFound()
    {
        var response = await _client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
