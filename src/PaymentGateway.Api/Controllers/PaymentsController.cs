using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Clients;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : Controller
{
    private readonly PaymentsRepository _paymentsRepository;
    private readonly PaymentRequestValidator _validator;
    private readonly IBankClient _bankClient;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        PaymentsRepository paymentsRepository,
        PaymentRequestValidator validator,
        IBankClient bankClient,
        ILogger<PaymentsController> logger)
    {
        _paymentsRepository = paymentsRepository;
        _validator = validator;
        _bankClient = bankClient;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType<PostPaymentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<RejectedPaymentResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> PostPaymentAsync(PostPaymentRequest request, CancellationToken cancellationToken)
    {
        var errors = _validator.Validate(request);
        if (errors.Count > 0)
            return BadRequest(new RejectedPaymentResponse { Errors = errors });

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


        return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, PostPaymentResponse.From(payment));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<GetPaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<GetPaymentResponse> GetPayment(Guid id)
    {
        var payment = _paymentsRepository.Get(id);
        if (payment is null)
            return NotFound();

        return GetPaymentResponse.From(payment);
    }
}
