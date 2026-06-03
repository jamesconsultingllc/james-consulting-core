using System;
using JamesConsulting.Internal;
using Microsoft.Extensions.Logging;

namespace JamesConsulting.Logging;

/// <summary>
/// A bounded, thread-safe, per-operation buffer of low-level log records. While a scope is active
/// (see <see cref="LogBuffer.BeginScope(int)" />), the buffering logger captures buffer-range
/// records into it instead of writing them. When the scope is flushed — either automatically by a
/// record at or above the configured flush level, or manually via <see cref="Flush" /> — every
/// captured record is emitted to its originating logger. Records are replayed in the order they were
/// logged within a single logical flow; ordering is best-effort under any concurrent logging on the
/// scope (for example a second thread flushing or writing live while a replay is in progress).
/// </summary>
/// <remarks>
/// <para>
/// The buffer is a ring buffer: once <c>capacity</c> records are held, the oldest record is dropped
/// to make room for the newest. Choose a capacity large enough to hold the diagnostic context you
/// want to see leading up to a failure.
/// </para>
/// <para>
/// Scopes are ambient and flow with <see cref="System.Threading.AsyncLocal{T}" />, so they cross
/// <c>await</c> points and <see cref="System.Threading.Tasks.Task" /> continuations within the same
/// logical operation. Always dispose a scope (preferably with <c>using</c>) at the end of the
/// operation to restore the previous ambient scope and stop capturing.
/// </para>
/// <para>
/// Buffered records do not capture <see cref="ILogger.BeginScope{TState}" /> state; logging scopes
/// active at capture time may already be disposed when the record is replayed. This matches the
/// behaviour of the built-in .NET log buffering.
/// </para>
/// </remarks>
public sealed class LogBufferScope : IDisposable
{
    /// <summary>
    /// The default ring-buffer capacity used by <see cref="LogBuffer.BeginScope()" /> when no
    /// explicit capacity is supplied.
    /// </summary>
    public const int DefaultCapacity = 1000;

    private readonly object gate = new();
    private readonly BufferedLogEntry?[] buffer;
    private readonly LogBufferScope? previous;
    private int head;
    private int count;
    private bool isFlushed;
    private bool isDisposed;

    internal LogBufferScope(LogBufferScope? previous, int capacity)
    {
        Guard.StrictlyPositive(capacity);
        this.previous = previous;
        buffer = new BufferedLogEntry?[capacity];
    }

    /// <summary>
    /// Gets the maximum number of records this scope can hold before the oldest are dropped.
    /// </summary>
    public int Capacity => buffer.Length;

    /// <summary>
    /// Gets the number of records currently held in the buffer.
    /// </summary>
    public int Count
    {
        get
        {
            lock (gate)
            {
                return count;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether this scope has been flushed, meaning subsequent buffer-range
    /// records are written live when <see cref="BufferingLoggerOptions.SuspendBufferingAfterFlush" />
    /// is enabled. Set by an error-triggered flush (even when the buffer was empty) and by a manual
    /// <see cref="Flush" /> that actually dumped records; an empty manual <see cref="Flush" /> leaves
    /// it unset. Ancestor scopes dumped for context by a nested error are <em>not</em> marked
    /// flushed.
    /// </summary>
    public bool IsFlushed
    {
        get
        {
            lock (gate)
            {
                return isFlushed;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether this scope has been disposed.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (gate)
            {
                return isDisposed;
            }
        }
    }

    /// <summary>
    /// Emits every buffered record, in the order it was logged, to its originating logger, then
    /// clears the buffer. When records are dumped the scope is marked flushed (suspending buffering
    /// for the remainder of the scope when
    /// <see cref="BufferingLoggerOptions.SuspendBufferingAfterFlush" /> is enabled); a manual flush
    /// that finds no buffered records is a no-op and does <em>not</em> suspend buffering, so a
    /// defensive <see cref="Flush" /> cannot silently disable buffering for the rest of the scope.
    /// Safe to call multiple times. Exceptions thrown while replaying an individual record are
    /// swallowed so that one faulty record cannot prevent the rest — or the triggering error — from
    /// being written.
    /// </summary>
    public void Flush() => FlushCore(suspendOnDump: true, suspendIfEmpty: false);

    /// <summary>
    /// Dumps the accrued context on the error-trigger path: ancestor scopes are dumped oldest-first
    /// for chronological context (their buffered records are consumed by this dump, but the ancestors
    /// are <em>not</em> marked flushed, so they keep buffering afterward and are not suspended), then
    /// this scope is dumped and always marked flushed — even when its buffer was empty — so an error
    /// suspends buffering for the remainder of this scope.
    /// </summary>
    internal void FlushForError()
    {
        previous?.DumpAncestorChain();
        FlushCore(suspendOnDump: true, suspendIfEmpty: true);
    }

    private void DumpAncestorChain()
    {
        previous?.DumpAncestorChain();
        FlushCore(suspendOnDump: false, suspendIfEmpty: false);
    }

    private void FlushCore(bool suspendOnDump, bool suspendIfEmpty)
    {
        BufferedLogEntry[] snapshot;
        lock (gate)
        {
            if (count == 0)
            {
                // Mark flushed (atomically, under the same lock) only when the caller wants an empty
                // flush to suspend — i.e. the error path. This closes the gap where a concurrent
                // enqueue between a separate flush and mark could be orphaned.
                if (suspendIfEmpty && !isDisposed)
                {
                    isFlushed = true;
                }

                return;
            }

            if (suspendOnDump)
            {
                isFlushed = true;
            }

            snapshot = new BufferedLogEntry[count];
            for (var i = 0; i < count; i++)
            {
                snapshot[i] = buffer[(head + i) % buffer.Length]!;
                buffer[(head + i) % buffer.Length] = null;
            }

            head = 0;
            count = 0;
        }

        // Replay outside the lock to avoid deadlocks/contention if a sink re-enters logging.
        foreach (var entry in snapshot)
        {
            try
            {
                entry.Replay();
            }
            catch
            {
                // Best-effort: a single faulty sink/record must not break the dump or suppress the
                // triggering error. The buffering logger is diagnostic infrastructure and never
                // throws back into the caller's logging path.
            }
        }
    }

    internal LogBufferEnqueueResult TryEnqueue(BufferedLogEntry entry, bool suspendAfterFlush)
    {
        lock (gate)
        {
            if (isDisposed)
            {
                return LogBufferEnqueueResult.Disposed;
            }

            if (isFlushed && suspendAfterFlush)
            {
                // The scope was flushed (possibly concurrently) and buffering is suspended: the
                // caller must emit this record live instead. Decided under the lock so it cannot
                // race with a concurrent Flush.
                return LogBufferEnqueueResult.AlreadyFlushed;
            }

            if (count == buffer.Length)
            {
                // Drop the oldest record to make room (ring buffer).
                buffer[head] = null;
                head = (head + 1) % buffer.Length;
                count--;
            }

            buffer[(head + count) % buffer.Length] = entry;
            count++;
            return LogBufferEnqueueResult.Enqueued;
        }
    }

    /// <summary>
    /// Disposes the scope, discarding any records that were buffered but never flushed, and restores
    /// the previously active ambient scope.
    /// </summary>
    public void Dispose()
    {
        lock (gate)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            for (var i = 0; i < count; i++)
            {
                buffer[(head + i) % buffer.Length] = null;
            }

            head = 0;
            count = 0;
        }

        LogBuffer.Restore(this, previous);
    }
}
