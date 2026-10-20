using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PaymentGateway.Api.Observability;

/// <summary>
/// assigns a correlation ID from X-Correlation-Id or the W3C trace ID, propagates it to the bank, echoes it in the response, and includes it in logs.
/// </summary>
public partial class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].ToString();
        var correlationId = SafeId().IsMatch(supplied)
            ? supplied
            : Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        context.Response.Headers[HeaderName] = correlationId;

        using (_logger.BeginScope("CorrelationId:{CorrelationId}", correlationId))
        {
            await _next(context);
        }
    }

    // caller-supplied values end up in logs so only accept short, plain identifiers.
    [GeneratedRegex("^[A-Za-z0-9._:-]{1,64}$")]
    private static partial Regex SafeId();
}
