using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using JamesConsulting.Internal;

namespace JamesConsulting.Hosting
{
    /// <summary>
    /// Provides extension methods to run initialization logic on an <see cref="IHost"/> instance.
    /// </summary>
    public static class HostExtensions
    {
        /// <summary>
        /// Runs synchronous <see cref="IHostInitializer"/> services registered in the host's service collection.
        /// </summary>
        /// <param name="host">The host whose services should be initialized.</param>
        /// <exception cref="ArgumentNullException"><paramref name="host"/> is <c>null</c>.</exception>
        /// <example>
        /// Register and invoke synchronous initializers.
        /// <code>
        /// var builder = Host.CreateDefaultBuilder();
        /// builder.ConfigureServices(s => s.AddTransient(typeof(IHostInitializer), typeof(MyInitializer)));
        /// using var host = builder.Build();
        /// host.Initialize();
        /// </code>
        /// </example>
        public static void Initialize(this IHost host)
        {
            Guard.NotNull(host);
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider.GetServices<IHostInitializer>();
            Parallel.ForEach(services, svc => svc.Initialize());
        }

        /// <summary>
        /// Runs asynchronous <see cref="IHostInitializerAsync"/> services registered in the host's service collection.
        /// </summary>
        /// <param name="host">The host whose services should be initialized asynchronously.</param>
        /// <returns>A <see cref="Task"/> representing completion of all asynchronous initializers.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="host"/> is <c>null</c>.</exception>
        /// <example>
        /// Register and invoke synchronous initializers.
        /// <code>
        /// var builder = Host.CreateDefaultBuilder();
        /// builder.ConfigureServices(s => s.AddTransient(typeof(IHostInitializer), typeof(MyInitializer)));
        /// using var host = builder.Build();
        /// await host.InitializeAsync();
        /// </code>
        /// </example>
        public static Task InitializeAsync(this IHost host)
        {
            Guard.NotNull(host);
            return host.InitializeInternalAsync();
        }

        private static async Task InitializeInternalAsync(this IHost host)
        {
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider.GetServices<IHostInitializerAsync>();
            await Task.Run(() => Parallel.ForEach(services, svc => svc.InitializeAsync())).ConfigureAwait(false);
        }
    }
}