# JamesConsulting

[![CI](https://github.com/jamesconsultingllc/james-consulting-core/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/jamesconsultingllc/james-consulting-core/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/JamesConsulting.svg?logo=nuget&label=NuGet)](https://www.nuget.org/packages/JamesConsulting/)
[![Downloads](https://img.shields.io/nuget/dt/JamesConsulting.svg?logo=nuget&label=Downloads)](https://www.nuget.org/packages/JamesConsulting/)
[![License: MIT](https://img.shields.io/github/license/jamesconsultingllc/james-consulting-core.svg)](LICENSE)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=jamesconsultingllc_james-consulting-core&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=jamesconsultingllc_james-consulting-core)
[![Docs](https://img.shields.io/badge/docs-jamesconsulting.biz-blue?logo=readthedocs)](https://docs.jamesconsulting.biz/james-consulting-core/)

> A small, opinionated grab-bag of extension methods, helpers, and primitives shared across all
> James Consulting LLC libraries. Multi-targets `netstandard2.0`, `netstandard2.1`, `net9.0`, and
> `net10.0` so it can drop into virtually any .NET project.

---

## Install

```bash
dotnet add package JamesConsulting
```

```xml
<PackageReference Include="JamesConsulting" Version="2.0.*" />
```

> **Trusted Publishing**: every `JamesConsulting` package on nuget.org is published via
> [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
> from this repo's `ci.yml` workflow (no long-lived API keys), and signed with
> [Azure Trusted Signing](https://learn.microsoft.com/en-us/azure/trusted-signing/overview) under
> the `James Consulting LLC` identity.

---

## What's included

| Namespace | Highlights |
|---|---|
| `JamesConsulting` | `StringExtensions`, `ByteArrayExtensions`, `EnumExtensions`, `ObjectExtensions` (mask/redact, deep-clone, JSON helpers), `Constants`, `MimeTypes`, `MethodTypeOptions` |
| `JamesConsulting.Cryptography` | Hashing/encoding helpers on top of `string` |
| `JamesConsulting.Hosting` | `HostExtensions`, `IHostInitializer` / `IHostInitializerAsync` — run async one-shot startup work inside `IHost` before `RunAsync()` |
| `JamesConsulting.IO` | `StreamExtensions` (read-to-end, copy with progress, etc.) |
| `JamesConsulting.Logging` | Buffering ("dump-on-error") logger — buffers Debug/Trace per scope and dumps them to your sinks only when an error is logged. See [Buffering logger](#buffering-dump-on-error-logger). |
| `JamesConsulting.Net` | `ConnectToSharedFolder` — UNC/SMB credential impersonation. `Connect()` is Windows-only and throws `PlatformNotSupportedException` on macOS/Linux; `Dispose()` and the finalizer are safe no-ops on non-Windows. |
| `JamesConsulting.Reflection` | `TypeExtensions` (default value resolution, async-method detection), `MethodInfoExtensions` |
| `JamesConsulting.Security` | `SecureStringExtensions`, additional `StringExtensions` |
| `JamesConsulting.Threading` | Async helpers on top of `MethodInfo` |

All public APIs ship with XML documentation; downstream consumers get IntelliSense + SourceLink for
step-into debugging.

📚 **Full API reference:** **<https://docs.jamesconsulting.biz/james-consulting-core/>** (generated with DocFX from `master`).

---

## Buffering ("dump-on-error") logger

`JamesConsulting.Logging` adds a logger that **buffers low-level logs in memory and only writes them
when something goes wrong**. During normal operation your sinks stay quiet; the moment an `Error` (or
`Critical`) is logged, the whole buffer of `Debug`/`Trace` context leading up to the failure is
dumped — so you get rich diagnostics exactly when you need them, without the noise (or cost) the rest
of the time.

This is the **auto-flush-on-error** trigger that .NET's built-in log buffering doesn't provide (its
buffer only flushes manually).

### How records are routed

Your host's existing live logging configuration (e.g. `Logging:LogLevel:Default`) stays **completely
authoritative** for what is written live — buffering never overrides it. On top of that, two
thresholds (`BufferLevel ≤ FlushLevel`) drive capture and the dump trigger:

| Record level | Behaviour |
|---|---|
| written live by your configuration | written live as normal (buffering doesn't touch it) |
| below the live threshold but `≥ BufferLevel` | captured into the active scope; emitted only on flush |
| below `BufferLevel` | dropped |
| `≥ FlushLevel`, inside a scope | flushes the scope (dumps the buffer + the triggering record) |
| `≥ FlushLevel`, no scope | follows your live configuration (nothing to dump) |

Defaults: `BufferLevel = Trace`, `FlushLevel = Error`. The live threshold is whatever your
`Logging:LogLevel` configuration already says.

### Usage

```csharp
using JamesConsulting.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddBufferingLogging(o =>
    {
        o.BufferLevel = LogLevel.Trace;  // how deep to capture
        o.FlushLevel = LogLevel.Error;   // what triggers the dump
    });
});
```

Your `Logging:LogLevel` configuration continues to control live logging unchanged — set it to
`Information` (or whatever you like) and `Debug`/`Trace` are buffered, then dumped on error.

Wrap a logical operation (request, message handler, job) in a buffering scope:

```csharp
public async Task ProcessAsync(ILogger<OrderService> logger, int orderId)
{
    using (LogBuffer.BeginScope())
    {
        logger.LogDebug("Loading order {OrderId}", orderId);     // buffered
        logger.LogInformation("Order {OrderId} loaded", orderId); // written live

        try
        {
            // ... work ...
        }
        catch (Exception ex)
        {
            // The buffered Debug record above is dumped first, then this error.
            logger.LogError(ex, "Failed to process order {OrderId}", orderId);
            throw;
        }
    }
    // No error? The Debug record is discarded when the scope is disposed.
}
```

> **Dumped records always reach your sinks — no filter configuration required.** The error dump is
> replayed **directly to the registered logging providers**, bypassing the
> Microsoft.Extensions.Logging factory-level filters (the `Logging:LogLevel` category/provider rules).
> That is deliberate: the whole point of a dump-on-error buffer is to surface the low-level context
> your live configuration suppresses, so a plain `Logging:LogLevel:Default = Information` setting is all
> you need — buffered `Debug`/`Trace` still appears on error. One consequence: an error replays the
> buffered context to **every** registered provider, even one whose own configured level would normally
> exclude those records.

> **Replay fidelity.** Object-valued structured properties (e.g. `logger.LogDebug("processing {Order}",
> order)`) are frozen to their `ToString()` text at log time so a later mutation can't change what the
> dump reports — a destructuring sink therefore receives the frozen text, not the live object. Scalar
> values (strings, numbers, enums, `DateTime`, `Guid`, …) keep their original type. Records are replayed
> in chronological order within a logical flow (nested scopes dump ancestors first); ordering is
> best-effort under concurrent logging on the same scope.

Buffering is AsyncLocal-scoped (it flows across `await`), thread-safe, and bounded by a ring buffer
(default capacity 1000, drop-oldest on overflow). Buffered records do **not** capture
`ILogger.BeginScope` state — matching the built-in .NET buffering.

---

## What's new in 2.0

This is a **breaking** modernization. If you're upgrading from `1.x`, read this list:

- **Dropped legacy target frameworks.** Now multi-targets `netstandard2.0`, `netstandard2.1`,
  `net9.0`, `net10.0`. Removed: `net5.0`, `net6.0`, `net7.0`, `net8.0`, `net462`, `netcoreapp3.1`.
- **Removed Metalama / PostSharp dependencies.** Argument validation that previously relied on
  Metalama contracts (`[Required]`, `[NotEmpty]`, `[StrictlyPositive]`, etc.) is now provided by an
  internal `Guard` helper modelled on `ArgumentNullException.ThrowIfNull`. Exception **types**
  (`ArgumentNullException` / `ArgumentException` / `ArgumentOutOfRangeException`) and `ParamName`
  values are preserved against the v1.x contract. **Exception messages and a few specific shapes
  changed** — most notably `MethodInfoExtensions.CreateTaskResult` now throws a clear
  `ArgumentException` for non-`Task<T>` return types instead of bubbling a reflection-internal
  `ArgumentException` out of `MakeGenericType`. The library no longer pulls Metalama into your
  build graph.
- **Trimmed transitive surface.** Single hosting reference (`Microsoft.Extensions.Hosting.Abstractions 10.0.0`).
- **macOS / Linux behavior.** `ConnectToSharedFolder.Connect()` throws
  `PlatformNotSupportedException` on non-Windows OSes (it wraps Win32 `WNetAddConnection2W`).
  `Dispose()` and the finalizer are safe no-ops on those platforms instead of
  `DllNotFoundException`-crashing tests.
- **Centralized package metadata** via `src/Directory.Build.props` (Authors, Company, Copyright,
  deterministic builds, embedded source, NBGV, SourceLink). No per-file copyright headers anymore.
- **Continuous deployment.** Single `ci.yml` GitHub Actions workflow replaces the old split
  build/publish workflows and the legacy Azure DevOps pipeline.

---

## Build & test

```bash
# from repo root
dotnet restore src/JamesConsulting.sln
dotnet build   src/JamesConsulting.sln --configuration Release
dotnet test    src/JamesConsulting.sln --configuration Release
```

Single TFM:

```bash
dotnet test src/JamesConsulting.Tests/JamesConsulting.Tests.csproj --framework net9.0
```

Filter to a single test:

```bash
dotnet test --filter "FullyQualifiedName~ObjectExtensionsTests.Mask"
```

---

## Contributing

1. Branch from `develop` (`feature/<name>` or `bugfix/<name>`, or a child of another development
   branch). All work targets `develop` — never branch from or push to `master`.
2. Tests first — this repo follows BDD/TDD with a 90% coverage target.
3. Run `dotnet build` and `dotnet test` locally before pushing.
4. Open a PR into `develop`. CI must be green and Sonar quality gate must pass.

> Releasing (cutting `release/**` branches, merging into `master`, and tagging) is restricted to the
> maintainer, James Consulting LLC. Contributions stop at a PR into `develop`.

Repo conventions live in [`AGENTS.md`](AGENTS.md).

---

## License

[MIT](LICENSE) © James Consulting LLC
