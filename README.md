# JamesConsulting

[![CI](https://github.com/jamesconsultingllc/james-consulting-core/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/jamesconsultingllc/james-consulting-core/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/JamesConsulting.svg?logo=nuget&label=NuGet)](https://www.nuget.org/packages/JamesConsulting/)
[![Downloads](https://img.shields.io/nuget/dt/JamesConsulting.svg?logo=nuget&label=Downloads)](https://www.nuget.org/packages/JamesConsulting/)
[![License: MIT](https://img.shields.io/github/license/jamesconsultingllc/james-consulting-core.svg)](LICENSE)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=jamesconsultingllc_james-consulting-core&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=jamesconsultingllc_james-consulting-core)

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
| `JamesConsulting.Hosting` | `IHostExtensions`, `IHostInitializer` / `IHostInitializerAsync` — run async one-shot startup work inside `IHost` before `RunAsync()` |
| `JamesConsulting.IO` | `StreamExtensions` (read-to-end, copy with progress, etc.) |
| `JamesConsulting.Net` | `ConnectToSharedFolder` — UNC/SMB credential impersonation (Windows-only at runtime; safe-no-op on macOS/Linux) |
| `JamesConsulting.Reflection` | `TypeExtensions` (default value resolution, async-method detection), `MethodInfoExtensions` |
| `JamesConsulting.Security` | `SecureStringExtensions`, additional `StringExtensions` |
| `JamesConsulting.Threading` | Async helpers on top of `MethodInfo` |

All public APIs ship with XML documentation; downstream consumers get IntelliSense + SourceLink for
step-into debugging.

---

## What's new in 2.0

This is a **breaking** modernization. If you're upgrading from `1.x`, read this list:

- **Dropped legacy target frameworks.** Now multi-targets `netstandard2.0`, `netstandard2.1`,
  `net9.0`, `net10.0`. Removed: `net5.0`, `net6.0`, `net7.0`, `net8.0`, `net462`, `netcoreapp3.1`.
- **Removed Metalama / PostSharp dependencies.** Argument validation that previously relied on
  Metalama contracts (`[Required]`, `[NotEmpty]`, `[StrictlyPositive]`, etc.) is now provided by an
  internal `Guard` helper modelled on `ArgumentNullException.ThrowIfNull`. Behaviour, exception
  types, and `ParamName`s are preserved — so consumers should see no observable difference at
  runtime, but the library no longer pulls Metalama into your build graph.
- **Trimmed transitive surface.** Single hosting reference (`Microsoft.Extensions.Hosting.Abstractions 10.0.0`).
- **macOS / Linux safety.** `ConnectToSharedFolder.Dispose()` and the finalizer now no-op on non-Windows
  OSes instead of `DllNotFoundException`-crashing tests under those runtimes.
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

## Release process

Releases follow GitFlow + semver. The `ci.yml` workflow has three jobs:

| Trigger | Job | Output |
|---|---|---|
| Any push / PR / dispatch | `build-test` | Build, test, Sonar, coverage, preview pack (`*-ci.<run_number>`). Runs on every push. |
| Push to `release/**` | `publish-rc` | Signs and publishes `<MAJOR>.<MINOR>.<PATCH>-rc.<run_number>` via NuGet Trusted Publishing. |
| Push tag `v<MAJOR>.<MINOR>.<PATCH>` | `publish-stable` | Verifies tag matches `version.json` *and* that the tagged commit is reachable from `master`, then signs and publishes. Also creates a GitHub Release. |

Cutting a release:

```bash
# 1. cut release branch from develop
git switch develop && git pull
git switch -c release/2.0.0
git push -u origin release/2.0.0
# -> publish-rc fires automatically: 2.0.0-rc.<run>

# 2. when QA approves, merge release/2.0.0 -> master, then tag from master
git switch master && git pull
git tag v2.0.0
git push origin v2.0.0
# -> publish-stable fires: 2.0.0 stable + GitHub Release

# 3. back-merge master -> develop
git switch develop
git merge --no-ff master
git push
```

> The major.minor.patch number lives in [`version.json`](version.json). Bump it before opening the
> release branch.

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
