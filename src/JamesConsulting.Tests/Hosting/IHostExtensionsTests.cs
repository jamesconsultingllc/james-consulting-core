using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JamesConsulting.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
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
        var serviceProvider = new Mock<IServiceProvider>();
        var serviceScopeFactory = new Mock<IServiceScopeFactory>();
        var serviceScope = new Mock<IServiceScope>();
        serviceScope.SetupGet(x => x.ServiceProvider).Returns(serviceProvider.Object);
        serviceScopeFactory.Setup(x => x.CreateScope()).Returns(serviceScope.Object);
        serviceProvider.Setup(x => x.GetService(typeof(IEnumerable<IHostInitializerAsync>)))
            .Returns(services.Select(x => x.Object));
        serviceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(serviceScopeFactory.Object);
        var host = new Mock<IHost>();
        host.SetupGet(x => x.Services).Returns(serviceProvider.Object);
        await host.Object.InitializeAsync();
        services.ForEach(x => x.Verify(y => y.InitializeAsync(), Times.Once()));
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
        var serviceProvider = new Mock<IServiceProvider>();
        var serviceScopeFactory = new Mock<IServiceScopeFactory>();
        var serviceScope = new Mock<IServiceScope>();
        serviceScope.SetupGet(x => x.ServiceProvider).Returns(serviceProvider.Object);
        serviceScopeFactory.Setup(x => x.CreateScope()).Returns(serviceScope.Object);
        serviceProvider.Setup(x => x.GetService(typeof(IEnumerable<IHostInitializer>)))
            .Returns(services.Select(x => x.Object));
        serviceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(serviceScopeFactory.Object);
        var host = new Mock<IHost>();
        host.SetupGet(x => x.Services).Returns(serviceProvider.Object);
        host.Object.Initialize();
        services.ForEach(x => x.Verify(y => y.Initialize(), Times.Once()));
    }

    /// <summary>
    /// Tests that the InitializeAsync method throws an ArgumentNullException when the host is null.
    /// </summary>
    [Fact]
    public void InitializeNullHostThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => default(IHost)!.Initialize());
    }

    private static List<Mock<T>> CreateInitializers<T>(int count)
        where T : class
    {
        var list = new List<Mock<T>>();

        for (var i = 0; i < count; i++) list.Add(new Mock<T>());

        return list;
    }
}