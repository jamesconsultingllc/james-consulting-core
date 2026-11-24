# JamesConsulting

[![CI](https://github.com/jamesconsultingllc/james-consulting-core/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/jamesconsultingllc/james-consulting-core/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/JamesConsulting.svg?logo=nuget&label=NuGet)](https://www.nuget.org/packages/JamesConsulting/)
[![Downloads](https://img.shields.io/nuget/dt/JamesConsulting.svg?logo=nuget&label=Downloads)](https://www.nuget.org/packages/JamesConsulting/)
[![License: MIT](https://img.shields.io/github/license/jamesconsultingllc/james-consulting-core.svg)](LICENSE)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=jamesconsultingllc_james-consulting-core&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=jamesconsultingllc_james-consulting-core)
[![Docs](https://img.shields.io/badge/docs-jamesconsulting.biz-blue?logo=readthedocs)](https://docs.jamesconsulting.biz/james-consulting-core/)

> A small, opinionated grab-bag of extension methods, helpers, and primitives shared across all
> James Consulting LLC libraries. Targets `net10.0` and `net11.0`.

---

## Install

Version 3.0 is in development. These examples install the currently published 2.x stable release.

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
| `JamesConsulting.Net` | `ConnectToSharedFolder` — UNC/SMB credential impersonation. `Connect()` is Windows-only and throws `PlatformNotSupportedException` on macOS/Linux; `Dispose()` and the finalizer are safe no-ops on non-Windows. |
| `JamesConsulting.Reflection` | `TypeExtensions` (default value resolution, async-method detection), `MethodInfoExtensions` |
| `JamesConsulting.Security` | `SecureStringExtensions`, additional `StringExtensions` |
| `JamesConsulting.Threading` | Async helpers on top of `MethodInfo` |

All public APIs ship with XML documentation; downstream consumers get IntelliSense + SourceLink for
step-into debugging.

📚 **Full API reference:** **<https://docs.jamesconsulting.biz/james-consulting-core/>** (generated with DocFX from `master`).

---

## 3.0 (in development)

- **Modern target frameworks.** Targets `net10.0` and `net11.0`; older frameworks are no longer supported.
- **Built-in JSON.** JSON helpers use `System.Text.Json` from the .NET shared framework, removing the
  Newtonsoft.Json and Utf8Json package dependencies without adding a package.
- **SDK-provided SourceLink.** Uses the SourceLink support built into the .NET SDK instead of pinning
  the obsolete 8.0.0 build-time package.
- **Mask selectors.** Dot-delimited properties, quoted bracket properties, array indices, and array
  wildcards are supported. Json.NET-specific filters and recursive-descent selectors are not.

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

Use the .NET 11 SDK, with the .NET 10 runtime installed to run tests for both targets.

```bash
# from repo root
dotnet restore src/JamesConsulting.sln
dotnet build   src/JamesConsulting.sln --configuration Release
dotnet test    src/JamesConsulting.sln --configuration Release
```

Single TFM:

```bash
dotnet test src/JamesConsulting.Tests/JamesConsulting.Tests.csproj --framework net11.0
```

Filter to a single test:

```bash
dotnet test --filter "FullyQualifiedName~ObjectExtensionsTests.Mask"
```

---

## Release process

Releases follow GitFlow + semver. NBGV reads the committed `version.json`; do not override
`Version`, `PackageVersion`, or `VersionSuffix` on the build command line. The `ci.yml` workflow
has three jobs:

| Trigger | Job | Output |
|---|---|---|
| Tracked branch push / PR / dispatch | `build-test` | Build, test, Sonar, coverage, and a CI-only package with NBGV's commit suffix (for example, `3.0.0-alpha.N.g<sha>`), using `PublicRelease=false`. |
| Push to `release/X.Y.Z` | `publish-rc` | Signs and publishes `X.Y.Z-rc.N` via NuGet Trusted Publishing. `N` is NBGV's Git version height, not the Actions run number; the version must match the release branch. |
| Push tag `vX.Y.Z` | `publish-stable` | Requires the computed version to equal `X.Y.Z` and the tagged commit to be reachable from `master`, then signs, publishes, and creates a GitHub Release. |

Before signing, both publishing jobs verify that exactly one package exists and that its ID,
embedded version, and filename match the expected release. Only that verified file is pushed.

The current development line is `3.0.0-alpha.{height}`. Cutting the 3.0 release:

```bash
# 1. cut release branch from develop
git switch develop && git pull
dotnet tool restore
git flow release start 3.0.0
dotnet nbgv set-version '3.0.0-rc.{height}'
git add version.json
git commit -m "chore(release): start 3.0.0 RC"
git push -u origin release/3.0.0
# -> publish-rc publishes 3.0.0-rc.N

# 2. when QA approves, set the stable version on the release branch
dotnet nbgv set-version 3.0.0
git add version.json
git commit -m "chore(release): finalize 3.0.0"
# Do NOT push this stable-version commit to release/3.0.0.
dotnet nbgv get-version -p src/JamesConsulting -v NuGetPackageVersion
# -> 3.0.0

# 3. finish: merge into master, tag v3.0.0, and back-merge into develop
git flow release finish 3.0.0
git push origin master develop --follow-tags
# -> publish-stable publishes 3.0.0 + GitHub Release

# 4. start the next development cycle
git switch develop
dotnet nbgv set-version '3.1.0-alpha.{height}'
git add version.json
git commit -m "chore(develop): start 3.1.0-alpha"
git push
```

Commit version changes before checking NBGV's output or packing: an uncommitted edit can combine
the new numeric version with prerelease settings and tags from the existing commit.

---

## Contributing

1. Branch from `develop` (`feature/<name>` or `bugfix/<name>`).
2. Tests first — this repo follows BDD/TDD with a 90% coverage target.
3. Run `dotnet build` and `dotnet test` locally before pushing.
4. Open a PR into `develop`. CI must be green and Sonar quality gate must pass.

Repo conventions live in [`AGENTS.md`](AGENTS.md).

---

## License

[MIT](LICENSE) © James Consulting LLC
