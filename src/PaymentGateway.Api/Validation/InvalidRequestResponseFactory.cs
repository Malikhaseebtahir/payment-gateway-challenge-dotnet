using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Validation;

/// <summary>
/// converts model binding errors into Rejected responses without exposing serializer details.
/// </summary>
public static class InvalidRequestResponseFactory
{
    public static IActionResult Create(ActionContext context)
    {
        var failedEntries = context.ModelState.Where(e => e.Value?.Errors.Count > 0).ToList();

        var jsonErrors = failedEntries
            .Where(e => e.Key.StartsWith('$'))
            .Select(e => e.Key.Length > 2 ? $"Field '{e.Key[2..]}' has an invalid value." : "The request body is not valid JSON.")
            .ToList();

        var errors = jsonErrors.Count > 0
            ? jsonErrors
            : failedEntries.SelectMany(e => e.Value!.Errors).Select(e => e.ErrorMessage).ToList();

        return new BadRequestObjectResult(new RejectedPaymentResponse { Errors = errors });
    }
}
