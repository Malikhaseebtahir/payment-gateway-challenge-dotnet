using System.Diagnostics.Metrics;

namespace PaymentGateway.Api.Observability;

///payment and bank latency metrics exposed via System.Diagnostics.Metrics for local monitoring and OpenTelemetry export.
public class PaymentMetrics
{
    public const string MeterName = "PaymentGateway.Api";

    private readonly Counter<long> _payments;
    private readonly Histogram<double> _bankDuration;

    public PaymentMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _payments = meter.CreateCounter<long>("payments.processed", description: "Payments processed, by outcome");
        _bankDuration = meter.CreateHistogram<double>("bank.request.duration", unit: "ms", description: "Acquiring bank call duration");
    }

    /// <param name="outcome">Authorized, Declined, Rejected or BankUnavailable.</param>
    public void PaymentProcessed(string outcome) =>
        _payments.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    public void BankRequestCompleted(double elapsedMilliseconds, bool succeeded) =>
        _bankDuration.Record(elapsedMilliseconds, new KeyValuePair<string, object?>("succeeded", succeeded));
}
