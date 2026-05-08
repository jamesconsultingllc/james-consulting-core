using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JamesConsulting.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace JamesConsulting.Tests.Hosting;

/// <summary>
/// The HostExtensionsTests class contains unit tests for the HostExtensions class.
/// </summary>
public class HostExtensionsTests
{
    /// <summary>
    /// Tests that the InitializeAsync method calls InitializeAsync on all IHostInitializerAsync instances.
    /// </summary>
    [Fact]
    public async Task InitializeAsyncCallInitializeOnHostInitializers()
    {
        var services = CreateInitializers<IHostInitializerAsync>(3);
        var host = BuildHost(services, (sp, instances) =>
            sp.GetService(typeof(IEnumerable<IHostInitializerAsync>)).Returns(instances));

        await host.InitializeAsync();

        foreach (var initializer in services)
            await initializer.Received(1).InitializeAsync();
    }

    [Fact]
    public async Task InitializeAsyncDoesNotCompleteUntilAllInitializersComplete()
    {
        var tcs1 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tcs2 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var init1 = Substitute.For<IHostInitializerAsync>();
        var init2 = Substitute.For<IHostInitializerAsync>();
        init1.InitializeAsync().Returns(tcs1.Task);
        init2.InitializeAsync().Returns(tcs2.Task);

        var host = BuildHost(new[] { init1, init2 }, (sp, instances) =>
            sp.GetService(typeof(IEnumerable<IHostInitializerAsync>)).Returns(instances));

        var pending = host.InitializeAsync();
        Assert.False(pending.IsCompleted, "InitializeAsync must not complete before all initializers complete.");

        tcs1.SetResult(true);
        Assert.False(pending.IsCompleted, "InitializeAsync must wait for every initializer, not the first one.");

        tcs2.SetResult(true);
        await pending;
        Assert.True(pending.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task InitializeAsyncNullHostThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => default(IHost)!.InitializeAsync());
    }

    /// <summary>
    /// Tests that the InitializeAsync method calls InitializeAsync on all IHostInitializerAsync instances.
    /// </summary>
    [Fact]
    public void InitializeCallInitializeOnHostInitializers()
    {
        var services = CreateInitializers<IHostInitializer>(3);
        var host = BuildHost(services, (sp, instances) =>
            sp.GetService(typeof(IEnumerable<IHostInitializer>)).Returns(instances));

        host.Initialize();

        foreach (var initializer in services)
            initializer.Received(1).Initialize();
    }

    /// <summary>
    /// Tests that the InitializeAsync method throws an ArgumentNullException when the host is null.
    /// </summary>
    [Fact]
    public void InitializeNullHostThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => default(IHost)!.Initialize());
    }

    private static IList<T> CreateInitializers<T>(int count)
        where T : class
    {
        var list = new List<T>();
        for (var i = 0; i < count; i++) list.Add(Substitute.For<T>());
        return list;
    }

    private static IHost BuildHost<T>(
        IEnumerable<T> initializers,
        Action<IServiceProvider, IEnumerable<T>> wireEnumerable)
        where T : class
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        var serviceScope = Substitute.For<IServiceScope>();
        serviceScope.ServiceProvider.Returns(serviceProvider);
        serviceScopeFactory.CreateScope().Returns(serviceScope);
        wireEnumerable(serviceProvider, initializers);
        serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);

        var host = Substitute.For<IHost>();
        host.Services.Returns(serviceProvider);
        return host;
    }
}