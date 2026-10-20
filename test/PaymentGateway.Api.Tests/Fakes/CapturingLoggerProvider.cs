using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace PaymentGateway.Api.Tests.Fakes;

/// <summary>captures all log messages, scopes, and exception text for assertions.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public ConcurrentQueue<string> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(CapturingLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => provider._scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var scopes = new List<string>();
            provider._scopes.ForEachScope((scope, list) => list.Add(FormatScope(scope)), scopes);
            provider.Entries.Enqueue($"{formatter(state, exception)} {exception} [{string.Join("; ", scopes)}]");
        }

        private static string FormatScope(object? scope) => scope is IEnumerable<KeyValuePair<string, object>> pairs
            ? string.Join(", ", pairs.Select(p => $"{p.Key}={p.Value}"))
            : scope?.ToString() ?? string.Empty;
    }
}
