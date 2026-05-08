namespace JamesConsulting.Hosting;

/// <summary>
/// Defines a synchronous host initialization contract invoked during application startup.
/// </summary>
public interface IHostInitializer
{
    /// <summary>
    /// Performs synchronous initialization logic (e.g. seeding caches, validating configuration).
    /// </summary>
    void Initialize();
}