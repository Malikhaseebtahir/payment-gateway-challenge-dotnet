using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Observability;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Produces("application/json")]
public class PaymentsController : Controller
{
    private readonly PaymentsRepository _paymentsRepository;
    private readonly PaymentRequestValidator _validator;
    private readonly IBankClient _bankClient;
    private readonly PaymentMetrics _metrics;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        PaymentsRepository paymentsRepository,
        PaymentRequestValidator validator,
        IBankClient bankClient,
        PaymentMetrics metrics,
        ILogger<PaymentsController> logger)
    {
        _paymentsRepository = paymentsRepository;
        _validator = validator;
        _bankClient = bankClient;
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>Processes a card payment through the acquiring bank.</summary>
    /// <remarks>
    /// The request is validated first, invalid requests are <b>Rejected</b> without contacting the bank and no
    /// payment is created, and valid requests are sent to the bank and the resulting <b>Authorized</b> or
    /// <b>Declined</b> payment is stored and can be retrieved with the returned id.
    /// </remarks>
    /// <response code="201">The bank authorized or declined the payment.</response>
    /// <response code="400">The payment was rejected because the request was invalid.</response>
    /// <response code="502">The acquiring bank was unavailable; no payment was created and it is safe to retry.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<PostPaymentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<RejectedPaymentResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> PostPaymentAsync(PostPaymentRequest request, CancellationToken cancellationToken)
    {
        var errors = _validator.Validate(request);
        if (errors.Count > 0)
        {
            _logger.LogInformation("Payment rejected with {ValidationErrorCount} validation errors: {ValidationErrors}", errors.Count, errors);
            _metrics.PaymentProcessed(PaymentStatus.Rejected.ToString());
            return BadRequest(new RejectedPaymentResponse { Errors = errors });
        }

        var bankRequest = new BankPaymentRequest(
            request.CardNumber!,
            $"{request.ExpiryMonth:D2}/{request.ExpiryYear}",
            request.Currency!,
            request.Amount,
            request.Cvv!);

        BankPaymentResponse bankResponse;
        try
        {
            bankResponse = await _bankClient.ProcessPaymentAsync(bankRequest, cancellationToken);
        }
        catch (BankUnavailableException ex)
        {
            _logger.LogError(ex, "Acquiring bank failed to process payment");
            _metrics.PaymentProcessed("BankUnavailable");
            return Problem(
                title: "Acquiring bank unavailable",
                detail: "The payment could not be processed. Please try again later.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        var payment = new Payment(
            Guid.NewGuid(),
            bankResponse.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            request.CardNumber![^4..],
            request.ExpiryMonth,
            request.ExpiryYear,
            request.Currency!,
            request.Amount);

        _paymentsRepository.Add(payment);

        // only the last four digits are ever logged; the full card number and CVV must never reach the logs.
        _logger.LogInformation(
            "Payment {PaymentId} {PaymentStatus} for {Amount} {Currency} on card ending {CardNumberLastFour}",
            payment.Id, payment.Status, payment.Amount, payment.Currency, payment.CardNumberLastFour);
        _metrics.PaymentProcessed(payment.Status.ToString());

        return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, PostPaymentResponse.From(payment));
    }

    /// <summary>Retrieves a previously processed payment.</summary>
    /// <param name="id">The payment id returned when the payment was processed.</param>
    /// <response code="200">The payment with only the last four card digits.</response>
    /// <response code="404">No payment exists with this id.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<GetPaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult<GetPaymentResponse> GetPayment(Guid id)
    {
        var payment = _paymentsRepository.Get(id);
        if (payment is null)
        {
            _logger.LogInformation("Payment {PaymentId} not found", id);
            return NotFound();
        }

        return GetPaymentResponse.From(payment);
    }
}
