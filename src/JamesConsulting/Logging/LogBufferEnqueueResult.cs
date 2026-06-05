namespace JamesConsulting.Logging;

/// <summary>
/// The outcome of attempting to enqueue a record into a <see cref="LogBufferScope" />.
/// </summary>
internal enum LogBufferEnqueueResult
{
    /// <summary>
    /// The record was buffered.
    /// </summary>
    Enqueued,

    /// <summary>
    /// The scope was already flushed and buffering is suspended, so the record was not buffered. The
    /// host's live configuration stays authoritative for it; because it is below the live threshold
    /// it is effectively dropped.
    /// </summary>
    AlreadyFlushed,

    /// <summary>
    /// The scope was disposed; the record was dropped.
    /// </summary>
    Disposed,
}
