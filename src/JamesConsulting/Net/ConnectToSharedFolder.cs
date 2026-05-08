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
    /// <param name="networkName">UNC path of the shared network folder. Must be non-null and non-empty.</param>
    /// <param name="credentials">Credentials used for impersonation. Must have a non-empty <see cref="NetworkCredential.UserName" />.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="networkName" /> or <paramref name="credentials" /> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="networkName" /> is empty or whitespace, or
    /// <paramref name="credentials" /><c>.UserName</c> is null or whitespace.
    /// </exception>
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
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer releasing the network connection. Shielded so any P/Invoke
    /// failure cannot escape the finalizer thread and crash the process.
    /// </summary>
    [ExcludeFromCodeCoverage]
    ~ConnectToSharedFolder()
    {
        Dispose(false);
    }

    [ExcludeFromCodeCoverage]
    private void Dispose(bool disposing)
    {
        // The constructor only assigns networkName after argument validation, so a
        // partially-initialized instance (ctor threw) leaves it null and we must
        // skip the native call. The finalizer must also never throw.
        if (string.IsNullOrEmpty(networkName)) return;
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        try
        {
            WNetCancelConnection2(networkName, 0, true);
        }
        catch
        {
            // Swallow all exceptions from native cleanup — failing to disconnect
            // here is non-fatal, and propagating from a finalizer would terminate
            // the process. Disposing path also swallows by symmetry; explicit
            // disconnection should use Connect()/Dispose() flow with surfaced
            // errors via the Connect() Win32Exception path.
            _ = disposing;
        }
    }

    /// <summary>
    /// Connects to the network share using the provided credentials.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">The current OS is not Windows.</exception>
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
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException(
                $"{nameof(ConnectToSharedFolder)}.{nameof(Connect)} requires Windows (uses mpr.dll P/Invoke).");

        var netResource = new NetResource
        {
            Scope = ResourceScope.GlobalNetwork,
            ResourceType = ResourceType.Disk,
            RemoteName = networkName
        };
        var userName = string.IsNullOrEmpty(credentials.Domain)
            ? credentials.UserName
            : $@"{credentials.Domain}\{credentials.UserName}";
        var result = WNetAddConnection2(netResource, credentials.Password, userName, 0);
        if (result != 0) throw new Win32Exception(result, "Error connecting to remote share");
    }

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetAddConnection2(NetResource netResource, string password, string username, int flags);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetCancelConnection2(string name, int flags, bool force);

    private enum ResourceScope
    {
        Connected = 1,
        GlobalNetwork = 2,
        Remembered = 3,
        Recent = 4,
        Context = 5
    }

    private enum ResourceType
    {
        Disk = 1
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    [ExcludeFromCodeCoverage]
    private sealed class NetResource
    {
        public ResourceScope Scope { get; set; }
        public ResourceType ResourceType { get; set; }
        public int DisplayType { get; set; }
        public int Usage { get; set; }
        public string? LocalName { get; set; }
        public string? RemoteName { get; set; }
        public string? Comment { get; set; }
        public string? Provider { get; set; }
    }
}