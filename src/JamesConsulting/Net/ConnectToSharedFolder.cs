using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.InteropServices;
using JamesConsulting.Internal;

namespace JamesConsulting.Net;

/// <summary>
/// Provides functionality to connect to a Windows network share using explicit credentials.
/// </summary>
public sealed class ConnectToSharedFolder : IDisposable
{
    private readonly NetworkCredential credentials;
    private readonly string networkName;

    /// <summary>
    /// Creates a new <see cref="ConnectToSharedFolder" /> instance.
    /// </summary>
    /// <param name="networkName">UNC path of the shared network folder.</param>
    /// <param name="credentials">Credentials used for impersonation.</param>
    /// <exception cref="ArgumentException">The credential user name is null or whitespace.</exception>
    /// <example>
    /// Construction and usage.
    /// <code>
    /// var creds = new NetworkCredential { UserName = "MyUser", Password = "Secret", Domain = "MYDOMAIN" };
    /// using var share = new ConnectToSharedFolder(@"\\server\share", creds);
    /// // share.Connect();
    /// </code>
    /// </example>
    public ConnectToSharedFolder(string networkName, NetworkCredential credentials)
    {
        Guard.Required(networkName);
        Guard.NotNull(credentials);
        if (string.IsNullOrWhiteSpace(credentials.UserName))
            throw new ArgumentException("UserName specified cannot be null or whitespace.", nameof(credentials));
        this.networkName = networkName;
        this.credentials = credentials;
    }

    /// <summary>
    /// Disposes the instance, disconnecting from the network share and suppressing finalization.
    /// </summary>
    /// <example>
    /// Dispose to disconnect.
    /// <code>
    /// var creds = new NetworkCredential { UserName = "MyUser" };
    /// var share = new ConnectToSharedFolder(@"\\server\share", creds);
    /// share.Dispose();
    /// </code>
    /// </example>
    [ExcludeFromCodeCoverage]
    public void Dispose()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WNetCancelConnection2(networkName, 0, true);
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer releasing the network connection.
    /// </summary>
    [ExcludeFromCodeCoverage]
    ~ConnectToSharedFolder()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WNetCancelConnection2(networkName, 0, true);
        }
    }

    /// <summary>
    /// Connects to the network share using the provided credentials.
    /// </summary>
    /// <exception cref="Win32Exception">The native API returns an error code.</exception>
    /// <example>
    /// Explicit connection.
    /// <code>
    /// var creds = new NetworkCredential { UserName = "MyUser", Password = "Secret" };
    /// using var share = new ConnectToSharedFolder(@"\\server\share", creds);
    /// share.Connect();
    /// </code>
    /// </example>
    [ExcludeFromCodeCoverage]
    public void Connect()
    {
        var netResource = new NetResource { Scope = ResourceScope.GlobalNetwork, ResourceType = ResourceType.Disk };
        var userName = string.IsNullOrEmpty(credentials.Domain)
            ? credentials.UserName
            : $@"{credentials.Domain}\\{credentials.UserName}";
        var result = WNetAddConnection2(netResource, credentials.Password, userName, 0);
        if (result != 0) throw new Win32Exception(result, "Error connecting to remote share");
    }

    [DllImport("mpr.dll")]
    private static extern int WNetAddConnection2(NetResource netResource, string password, string username, int flags);

    [DllImport("mpr.dll")]
    private static extern int WNetCancelConnection2(string name, int flags, bool force);

    private enum ResourceScope
    {
        GlobalNetwork
    }

    private enum ResourceType
    {
        Disk = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    [ExcludeFromCodeCoverage]
    private sealed class NetResource
    {
        public ResourceScope Scope { get; set; }
        public ResourceType ResourceType { get; set; }
    }
}