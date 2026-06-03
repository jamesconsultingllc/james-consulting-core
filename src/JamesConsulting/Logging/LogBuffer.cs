using System.Threading;

namespace JamesConsulting.Logging;

/// <summary>
/// Entry point for starting and accessing the ambient <see cref="LogBufferScope" /> used by the
/// buffering logger. Wrap a logical operation (an HTTP request, a message handler, a background job)
/// in a scope so that low-level diagnostic logs are captured and only emitted if the operation
/// fails.
/// </summary>
/// <example>
/// Buffer Debug/Trace logs for the duration of an operation and dump them on error:
/// <code>
/// using (LogBuffer.BeginScope())
/// {
///     logger.LogDebug("Loading order {OrderId}", orderId);   // buffered
///     logger.LogInformation("Order {OrderId} loaded", orderId); // written live
///     // ... if an error is logged here, the buffered Debug record is dumped first ...
///     logger.LogError(ex, "Failed to process order {OrderId}", orderId);
/// }
/// </code>
/// </example>
public static class LogBuffer
{
    private static readonly AsyncLocal<LogBufferScope?> CurrentScope = new();

    /// <summary>
    /// Gets the scope currently active on this asynchronous flow, or <c>null</c> if none is active.
    /// </summary>
    public static LogBufferScope? Current => CurrentScope.Value;

    /// <summary>
    /// Starts a new buffering scope using <see cref="LogBufferScope.DefaultCapacity" /> and makes it
    /// the ambient scope for the current asynchronous flow.
    /// </summary>
    /// <returns>The new scope. Dispose it (preferably with <c>using</c>) to end buffering.</returns>
    public static LogBufferScope BeginScope() => BeginScope(LogBufferScope.DefaultCapacity);

    /// <summary>
    /// Starts a new buffering scope with the specified ring-buffer capacity and makes it the ambient
    /// scope for the current asynchronous flow. Scopes may be nested; disposing a nested scope
    /// restores its parent.
    /// </summary>
    /// <param name="capacity">The maximum number of records the scope buffers before the oldest are dropped.</param>
    /// <returns>The new scope. Dispose it (preferably with <c>using</c>) to end buffering.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="capacity" /> is not greater than zero.</exception>
    public static LogBufferScope BeginScope(int capacity)
    {
        var scope = new LogBufferScope(CurrentScope.Value, capacity);
        CurrentScope.Value = scope;
        return scope;
    }

    /// <summary>
    /// Restores the previous ambient scope when <paramref name="scope" /> is disposed. Uses a
    /// best-effort, last-in-first-out restore: the ambient slot is only changed when it still refers
    /// to <paramref name="scope" />, so out-of-order disposal does not clobber an unrelated scope.
    /// </summary>
    internal static void Restore(LogBufferScope scope, LogBufferScope? previous)
    {
        if (ReferenceEquals(CurrentScope.Value, scope))
        {
            CurrentScope.Value = previous;
        }
    }
}
