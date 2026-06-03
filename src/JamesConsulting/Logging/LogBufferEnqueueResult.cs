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
    /// The scope was already flushed and buffering is suspended; the caller should emit the record
    /// live instead of buffering it.
    /// </summary>
    AlreadyFlushed,

    /// <summary>
    /// The scope was disposed; the record was dropped.
    /// </summary>
    Disposed,
}
