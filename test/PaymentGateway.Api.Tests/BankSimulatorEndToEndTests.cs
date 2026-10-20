using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc.Testing;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Tests;

/// <summary>
/// runs the real gateway (including the real BankClient) against the Mountebank bank simulator.
/// start it with <c>docker compose up -d bank_simulator</c> these tests are skipped when it isn't reachable,
/// so <c>dotnet test</c> still passes on a machine without Docker.
/// </summary>
[Trait("Category", "EndToEnd")]
public class BankSimulatorEndToEndTests
{
    private static readonly Uri SimulatorUrl = new("http://localhost:8080/");
    private static readonly Lazy<bool> SimulatorAvailable = new(() =>
    {
        try
        {
            using var tcp = new TcpClient();
            return tcp.ConnectAsync(SimulatorUrl.Host, SimulatorUrl.Port).Wait(TimeSpan.FromSeconds(1));
        }
        catch (Exception)
        {
            return false;
        }
    });

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client = new WebApplicationFactory<Program>().CreateClient();

    private static PostPaymentRequest Request(string cardNumber) => new()
    {
        CardNumber = cardNumber,
        ExpiryMonth = 4,
        ExpiryYear = DateTime.UtcNow.Year + 1,
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

    [SkippableTheory]
    [InlineData("2222405343248877", PaymentStatus.Authorized)]
    [InlineData("2222405343248112", PaymentStatus.Declined)]
    public async Task ProcessesAndRetrievesPayment(string cardNumber, PaymentStatus expectedStatus)
    {
        Skip.IfNot(SimulatorAvailable.Value, "Bank simulator is not running on localhost:8080");

        var postResponse = await _client.PostAsJsonAsync("/api/Payments", Request(cardNumber));
        var created = await postResponse.Content.ReadFromJsonAsync<PostPaymentResponse>(JsonOptions);
        var getResponse = await _client.GetAsync(postResponse.Headers.Location);
        var retrieved = await getResponse.Content.ReadFromJsonAsync<GetPaymentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        Assert.Equal(expectedStatus, created!.Status);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(created.Id, retrieved!.Id);
        Assert.Equal(expectedStatus, retrieved.Status);
        Assert.Equal(cardNumber[^4..], retrieved.CardNumberLastFour);
    }

    [SkippableFact]
    public async Task Returns502WhenSimulatorIsUnavailable()
    {
        Skip.IfNot(SimulatorAvailable.Value, "Bank simulator is not running on localhost:8080");

        var response = await _client.PostAsJsonAsync("/api/Payments", Request("2222405343248870"));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }
}
