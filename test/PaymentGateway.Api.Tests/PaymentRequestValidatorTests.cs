using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Tests.Fakes;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Tests;

public class PaymentRequestValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly PaymentRequestValidator _validator = new(new FixedTimeProvider(Now));

    private static PostPaymentRequest ValidRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 4,
        ExpiryYear = 2027,
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

    [Fact]
    public void AcceptsValidRequest()
    {
        Assert.Empty(_validator.Validate(ValidRequest()));
    }

    [Theory]
    [InlineData("12345678901234")]
    [InlineData("1234567890123456789")]
    public void AcceptsCardNumberAtLengthBounds(string cardNumber)
    {
        var request = ValidRequest();
        request.CardNumber = cardNumber;

        Assert.Empty(_validator.Validate(request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567890123")]
    [InlineData("12345678901234567890")]
    [InlineData("2222-4053-4324-8877")]
    [InlineData("222240534324887a")]
    public void RejectsInvalidCardNumber(string? cardNumber)
    {
        var request = ValidRequest();
        request.CardNumber = cardNumber;

        Assert.Contains(_validator.Validate(request), e => e.StartsWith("Card number"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void RejectsInvalidExpiryMonth(int month)
    {
        var request = ValidRequest();
        request.ExpiryMonth = month;

        Assert.Contains(_validator.Validate(request), e => e.StartsWith("Expiry month"));
    }

    [Theory]
    [InlineData(5, 2026)]
    [InlineData(12, 2025)]
    [InlineData(6, 0)]
    public void RejectsExpiryDateInThePast(int month, int year)
    {
        var request = ValidRequest();
        request.ExpiryMonth = month;
        request.ExpiryYear = year;

        Assert.Contains("Expiry date must be in the future.", _validator.Validate(request));
    }

    [Theory]
    [InlineData(6, 2026)]
    [InlineData(7, 2026)]
    [InlineData(1, 2027)]
    public void AcceptsExpiryDateInTheFuture(int month, int year)
    {
        var request = ValidRequest();
        request.ExpiryMonth = month;
        request.ExpiryYear = year;

        Assert.Empty(_validator.Validate(request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("GB")]
    [InlineData("GBPP")]
    [InlineData("JPY")]
    [InlineData("gbp")]
    public void RejectsInvalidCurrency(string? currency)
    {
        var request = ValidRequest();
        request.Currency = currency;

        Assert.Contains(_validator.Validate(request), e => e.StartsWith("Currency"));
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void AcceptsSupportedCurrency(string currency)
    {
        var request = ValidRequest();
        request.Currency = currency;

        Assert.Empty(_validator.Validate(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void RejectsNonPositiveAmount(int amount)
    {
        var request = ValidRequest();
        request.Amount = amount;

        Assert.Contains(_validator.Validate(request), e => e.StartsWith("Amount"));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1234")]
    [InlineData("012")]
    public void AcceptsValidCvv(string cvv)
    {
        var request = ValidRequest();
        request.Cvv = cvv;

        Assert.Empty(_validator.Validate(request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("12a")]
    public void RejectsInvalidCvv(string? cvv)
    {
        var request = ValidRequest();
        request.Cvv = cvv;

        Assert.Contains(_validator.Validate(request), e => e.StartsWith("CVV"));
    }

    [Fact]
    public void ReportsEveryInvalidField()
    {
        Assert.Equal(5, _validator.Validate(new PostPaymentRequest()).Count);
    }
}
