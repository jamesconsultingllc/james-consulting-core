namespace JamesConsulting;

/// <summary>
/// The method type options.
/// </summary>
public enum MethodTypeOptions
{
    /// <summary>
    /// A synchronous method returning <c>void</c>.
    /// </summary>
    SyncAction,

    /// <summary>
    /// A synchronous method returning a value.
    /// </summary>
    SyncFunction,

    /// <summary>
    /// An asynchronous method returning a <see cref="System.Threading.Tasks.Task" /> with no result.
    /// </summary>
    AsyncAction,

    /// <summary>
    /// An asynchronous method returning a <see cref="System.Threading.Tasks.Task{TResult}" /> result.
    /// </summary>
    AsyncFunction
}