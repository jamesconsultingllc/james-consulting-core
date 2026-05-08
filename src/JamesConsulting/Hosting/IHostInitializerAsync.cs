using System.Threading.Tasks;

namespace JamesConsulting.Hosting;

/// <summary>
/// The HostInitializer interface.
/// Defines an asynchronous host initialization contract invoked during application startup.
/// </summary>
public interface IHostInitializerAsync
{
    /// <summary>
    /// The initialize async.
    /// Performs asynchronous initialization logic (e.g. warm-up calls, remote configuration retrieval).
    /// </summary>
    /// <returns>
    /// A <see cref="Task" /> representing the asynchronous initialization operation.
    /// </returns>
    Task InitializeAsync();
}