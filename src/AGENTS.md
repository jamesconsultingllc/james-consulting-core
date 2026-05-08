# Class Library Agent Instructions (C# / .NET)

> **Library-specific directives.** The shared rules at the [repo root AGENTS.md](../AGENTS.md) also apply.
>
> **This is a reusable NuGet package, NOT an application.** Libraries expose hooks — they never configure infrastructure.

---

## Golden Rule

**A library must be usable with zero observability configured and still function correctly.**

- No Serilog / sink configuration
- No OTel exporter setup
- No `IHost` / `IHostBuilder` usage
- No HTTP status code awareness
- No middleware registration
- No `appsettings.json` reading (use `IOptions<T>` pattern)

The consuming application owns all configuration. The library provides the building blocks.

---

## Required Packages

### Core

| Package | Purpose |
|---------|---------|
| `Microsoft.Extensions.Logging.Abstractions` | `ILogger<T>` interface (no concrete sinks) |
| `Microsoft.Extensions.Options` | `IOptions<T>` for configuration |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `IServiceCollection` extension methods |
| `System.Diagnostics.DiagnosticSource` | `ActivitySource` + `Meter` (built into .NET) |
| `MemoryPack` | High-performance binary serialization (if DTOs are shared) |
| `FluentValidation` | Validation rules (NOT `FluentValidation.AspNetCore`) |

### Testing

| Package | Purpose |
|---------|---------|
| `xUnit` | Unit test framework |
| `FluentAssertions` | Readable assertion syntax |
| `NSubstitute` | Mocking framework |
| `Bogus` | Realistic test data generation |
| `Reqnroll` + `Reqnroll.xUnit` | BDD / Gherkin `.feature` file runner |
| `Verify.Xunit` | Snapshot testing for complex outputs |
| `NetArchTest.Rules` | Enforce architecture rules |
| `Microsoft.Extensions.Logging.Testing` | `FakeLogger<T>` for asserting log output |
| `Microsoft.Extensions.Options` | `Options.Create<T>()` for test configuration |

### Code Quality

| Package | Purpose |
|---------|---------|
| `Microsoft.CodeAnalysis.PublicApiAnalyzers` | Track public API surface changes |
| `MinVer` or `GitVersion` | SemVer from git tags |
| `Microsoft.SourceLink.GitHub` | Source link for debugging NuGet consumers |

---

## Project Structure

```
src/
├── MyLib/
│   ├── MyLib.csproj
│   ├── Features/
│   │   ├── Parsing/
│   │   │   ├── DocumentParser.cs
│   │   │   ├── ParsingOptions.cs         # IOptions<T> config
│   │   │   └── ParsingErrors.cs          # Error code constants
│   │   └── Validation/
│   │       ├── RuleEngine.cs
│   │       └── RuleEngineErrors.cs
│   ├── Telemetry/
│   │   ├── LibraryActivitySources.cs     # Named ActivitySource constants
│   │   └── LibraryMeters.cs              # Named Meter constants
│   ├── DependencyInjection/
│   │   └── ServiceCollectionExtensions.cs # AddMyLib() registration
│   └── PublicAPI.Shipped.txt             # API surface tracking
│   └── PublicAPI.Unshipped.txt
tests/
├── MyLib.UnitTests/
│   └── Features/
│       ├── Parsing/
│       │   ├── DocumentParserTests.cs
│       │   └── Parsing.feature
│       └── Validation/
│           └── RuleEngineTests.cs
├── MyLib.ArchTests/
│   └── ArchitectureTests.cs
└── MyLib.IntegrationTests/
    └── ...
```

---

## Multi-Targeting

Support the two most recent .NET LTS/current versions:

```xml
<PropertyGroup>
  <TargetFrameworks>net9.0;net10.0</TargetFrameworks>
  <LangVersion>latest</LangVersion>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

### Conditional Compilation

When using APIs only available in newer targets:

```csharp
#if NET10_0_OR_GREATER
    // Use new .NET 10 API
    return span.TrySplit(separator, out var left, out var right);
#else
    // Fallback for .NET 9
    var index = span.IndexOf(separator);
    if (index < 0) return false;
    left = span[..index];
    right = span[(index + 1)..];
    return true;
#endif
```

---

## NuGet Packaging

### Project File Metadata

```xml
<PropertyGroup>
  <PackageId>MyCompany.MyLib</PackageId>
  <Authors>My Company</Authors>
  <Description>Brief description of what the library does.</Description>
  <PackageTags>relevant;tags;here</PackageTags>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <RepositoryUrl>https://github.com/mycompany/mylib</RepositoryUrl>

  <!-- Source Link + deterministic builds -->
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
  <EmbedUntrackedSources>true</EmbedUntrackedSources>
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  <Deterministic>true</Deterministic>
  <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
</PropertyGroup>

<ItemGroup>
  <None Include="../../README.md" Pack="true" PackagePath="/" />
</ItemGroup>
```

### SemVer Rules

| Change | Version Bump | Examples |
|--------|-------------|----------|
| Bug fix, perf improvement | Patch (`1.0.x`) | Fix null check, optimize hot path |
| New public API (backward compatible) | Minor (`1.x.0`) | Add new method, new overload |
| Breaking change to public API | Major (`x.0.0`) | Remove method, change signature, rename type |

### API Surface Management

Use `PublicApiAnalyzers` to track breaking changes:

```
# PublicAPI.Shipped.txt — committed public API surface
MyLib.DocumentParser.Parse(string) -> MyLib.ParseResult
MyLib.ParseResult.IsSuccess.get -> bool
MyLib.ParseResult.Errors.get -> System.Collections.Generic.IReadOnlyList<string>
```

**Never** remove entries from `PublicAPI.Shipped.txt` without a major version bump.

---

## Logging (Library Pattern)

Libraries accept `ILogger<T>` via DI and use `[LoggerMessage]` source generator. They **never** configure sinks.

```csharp
/// <summary>
/// Source-generated log messages for the parsing feature.
/// Zero-allocation, compile-time checked, unique EventIds.
/// </summary>
public static partial class ParsingLogs
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Information,
        Message = "Document {DocumentId} parsed in {ElapsedMs}ms — {PageCount} pages")]
    public static partial void DocumentParsed(
        ILogger logger, string documentId, double elapsedMs, int pageCount);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning,
        Message = "Document {DocumentId} parse failed: {Reason}")]
    public static partial void DocumentParseFailed(
        ILogger logger, string documentId, string reason);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Debug,
        Message = "Parsing page {PageNumber}/{TotalPages} of document {DocumentId}")]
    public static partial void ParsingPage(
        ILogger logger, int pageNumber, int totalPages, string documentId);
}

// Usage — consumer provides the ILogger, library just uses it
public class DocumentParser
{
    private readonly ILogger<DocumentParser> _logger;

    public DocumentParser(ILogger<DocumentParser> logger)
    {
        _logger = logger;
    }

    public ParseResult Parse(string documentId, Stream content)
    {
        var sw = Stopwatch.StartNew();
        // ... parsing logic ...
        ParsingLogs.DocumentParsed(_logger, documentId, sw.Elapsed.TotalMilliseconds, pageCount);
        return result;
    }
}
```

### EventId Ranges

Assign non-overlapping EventId ranges per feature to avoid collisions:

| Feature | EventId Range |
|---------|--------------|
| Parsing | 2001–2099 |
| Validation | 2100–2199 |
| Caching | 2200–2299 |

---

## Telemetry (Library Pattern)

Libraries **expose** named `ActivitySource` and `Meter` instances. They **never** configure exporters.

### Expose Named Sources

```csharp
/// <summary>
/// Named ActivitySource and Meter for the library.
/// Consumers register these with their OTel pipeline:
///   .WithTracing(b => b.AddSource(LibraryActivitySources.Parsing))
///   .WithMetrics(b => b.AddMeter(LibraryMeters.Parsing))
/// </summary>
public static class LibraryActivitySources
{
    /// <summary>Tracing source for document parsing operations.</summary>
    public const string Parsing = "MyCompany.MyLib.Parsing";

    /// <summary>Tracing source for validation operations.</summary>
    public const string Validation = "MyCompany.MyLib.Validation";

    /// <summary>All sources — for easy bulk registration by consumers.</summary>
    public static readonly IReadOnlyList<string> All = [Parsing, Validation];
}

public static class LibraryMeters
{
    /// <summary>Meter for parsing metrics.</summary>
    public const string Parsing = "MyCompany.MyLib.Parsing";

    /// <summary>All meters — for easy bulk registration by consumers.</summary>
    public static readonly IReadOnlyList<string> All = [Parsing];
}
```

### Usage in Library Code

```csharp
public class DocumentParser
{
    private static readonly ActivitySource s_activity = new(LibraryActivitySources.Parsing);
    private static readonly Meter s_meter = new(LibraryMeters.Parsing);
    private static readonly Counter<long> s_docsParsed = s_meter.CreateCounter<long>(
        "mylib.documents.parsed", "documents", "Total documents parsed");
    private static readonly Histogram<double> s_parseDuration = s_meter.CreateHistogram<double>(
        "mylib.documents.parse_duration", "ms", "Document parse duration");

    public ParseResult Parse(string documentId, Stream content)
    {
        using var activity = s_activity.StartActivity("ParseDocument");
        activity?.SetTag("document.id", documentId);

        var sw = Stopwatch.StartNew();
        try
        {
            // ... parsing logic ...
            s_docsParsed.Add(1);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
        finally
        {
            s_parseDuration.Record(sw.Elapsed.TotalMilliseconds);
            activity?.SetTag("document.page_count", pageCount);
        }
    }
}
```

### Document Registration for Consumers

In your library's README and DI extension:

```csharp
/// <summary>
/// Registers MyLib services. Call in the consuming application's DI setup.
/// </summary>
/// <example>
/// services.AddMyLib(options => { options.MaxPageSize = 100; });
///
/// // Register telemetry sources:
/// services.AddOpenTelemetry()
///     .WithTracing(b => b.AddSource(LibraryActivitySources.All.ToArray()))
///     .WithMetrics(b => b.AddMeter(LibraryMeters.All.ToArray()));
/// </example>
public static IServiceCollection AddMyLib(
    this IServiceCollection services,
    Action<MyLibOptions>? configure = null)
{
    if (configure is not null)
        services.Configure(configure);

    services.AddSingleton<DocumentParser>();
    services.AddSingleton<RuleEngine>();
    return services;
}
```

---

## Error Codes (Library Pattern)

Libraries define domain error codes as **constants**. They never map to HTTP status codes — that's the consuming application's job.

### Pattern: Error Code Constants

```csharp
/// <summary>
/// Error codes for the parsing feature.
/// Consuming applications map these to HTTP status codes and localized messages.
/// </summary>
public static class ParsingErrors
{
    public const string DocumentTooLarge = "PARSING_DOCUMENT_TOO_LARGE";
    public const string UnsupportedFormat = "PARSING_UNSUPPORTED_FORMAT";
    public const string CorruptedContent = "PARSING_CORRUPTED_CONTENT";
    public const string PageLimitExceeded = "PARSING_PAGE_LIMIT_EXCEEDED";
}

public static class ValidationErrors
{
    public const string RuleNotFound = "VALIDATION_RULE_NOT_FOUND";
    public const string InvalidExpression = "VALIDATION_INVALID_EXPRESSION";
}
```

### Pattern: Typed Exceptions (No HTTP)

```csharp
/// <summary>
/// Base exception for library errors. Contains a domain error code.
/// The consuming application's exception middleware maps these to HTTP responses.
/// </summary>
public class MyLibException : Exception
{
    /// <summary>Domain error code (e.g., PARSING_DOCUMENT_TOO_LARGE).</summary>
    public string Code { get; }

    /// <summary>Optional structured details for the error.</summary>
    public object? Details { get; }

    public MyLibException(string code, string message, object? details = null)
        : base(message)
    {
        Code = code;
        Details = details;
    }
}

/// <summary>Thrown when a document cannot be parsed.</summary>
public class ParsingException : MyLibException
{
    public ParsingException(string code, string message, object? details = null)
        : base(code, message, details) { }
}

// Usage in library code:
throw new ParsingException(
    ParsingErrors.DocumentTooLarge,
    $"Document exceeds maximum size of {maxBytes} bytes",
    new { ActualSize = content.Length, MaxSize = maxBytes });
```

### FluentValidation with Error Codes

```csharp
/// <summary>
/// Validation rules for parsing options.
/// Every rule has a .WithErrorCode() — no exceptions.
/// </summary>
public class ParsingOptionsValidator : AbstractValidator<ParsingOptions>
{
    public ParsingOptionsValidator()
    {
        RuleFor(x => x.MaxPageSize)
            .GreaterThan(0)
            .WithErrorCode(ParsingErrors.PageLimitExceeded)
            .WithMessage("MaxPageSize must be greater than 0");

        RuleFor(x => x.SupportedFormats)
            .NotEmpty()
            .WithErrorCode(ParsingErrors.UnsupportedFormat)
            .WithMessage("At least one supported format is required");
    }
}
```

---

## Configuration (IOptions Pattern)

Libraries accept configuration via `IOptions<T>`, never by reading files directly:

```csharp
/// <summary>
/// Configuration options for the library.
/// Consumers bind this from appsettings.json, environment variables, etc.
/// </summary>
public class MyLibOptions
{
    /// <summary>Maximum number of pages to parse per document.</summary>
    public int MaxPageSize { get; set; } = 100;

    /// <summary>Supported document formats.</summary>
    public List<string> SupportedFormats { get; set; } = ["pdf", "docx", "xlsx"];

    /// <summary>Enable detailed parsing telemetry (debug-level spans).</summary>
    public bool DetailedTelemetry { get; set; } = false;
}
```

---

## MemoryPack (Library DTOs)

If the library exposes DTOs shared between client and server:

```csharp
/// <summary>
/// Version-tolerant DTO for cross-service use.
/// Use [MemoryPackOrder] for safe schema evolution.
/// </summary>
[MemoryPackable(GenerateType.VersionTolerant)]
public partial class ParseResult
{
    [MemoryPackOrder(0)] public bool IsSuccess { get; set; }
    [MemoryPackOrder(1)] public int PageCount { get; set; }
    [MemoryPackOrder(2)] public IReadOnlyList<string> Errors { get; set; } = [];
    [MemoryPackOrder(3)] public byte[]? OutputData { get; set; }
}
```

---

## Architecture Tests

```csharp
/// <summary>
/// Library must not depend on ASP.NET Core, hosting, or HTTP abstractions.
/// </summary>
[Fact]
public void Library_ShouldNot_DependOn_AspNetCore()
{
    Types.InAssembly(typeof(DocumentParser).Assembly)
        .ShouldNot()
        .HaveDependencyOnAny(
            "Microsoft.AspNetCore",
            "Microsoft.Extensions.Hosting",
            "System.Net.Http")
        .GetResult()
        .IsSuccessful.Should().BeTrue();
}

/// <summary>
/// All public ActivitySource and Meter names must follow the naming convention.
/// </summary>
[Fact]
public void TelemetrySources_ShouldFollow_NamingConvention()
{
    foreach (var source in LibraryActivitySources.All)
        source.Should().StartWith("MyCompany.MyLib.");

    foreach (var meter in LibraryMeters.All)
        meter.Should().StartWith("MyCompany.MyLib.");
}

/// <summary>
/// All exception classes must inherit from MyLibException.
/// </summary>
[Fact]
public void Exceptions_ShouldInherit_MyLibException()
{
    Types.InAssembly(typeof(DocumentParser).Assembly)
        .That().Inherit(typeof(Exception))
        .And().DoNotHaveNameMatching("MyLibException")
        .Should().Inherit(typeof(MyLibException))
        .GetResult()
        .IsSuccessful.Should().BeTrue();
}
```

---

## Documentation

### XML Docs Required for ALL Public APIs

```csharp
/// <summary>
/// Parses a document from the provided stream.
/// </summary>
/// <param name="documentId">Unique identifier for correlation and telemetry.</param>
/// <param name="content">Document content stream. Caller is responsible for disposal.</param>
/// <returns>Parse result indicating success/failure with page count and errors.</returns>
/// <exception cref="ParsingException">
/// Thrown with <see cref="ParsingErrors.DocumentTooLarge"/> when content exceeds max size.
/// Thrown with <see cref="ParsingErrors.UnsupportedFormat"/> when format is not recognized.
/// </exception>
/// <example>
/// <code>
/// var parser = serviceProvider.GetRequiredService&lt;DocumentParser&gt;();
/// using var stream = File.OpenRead("document.pdf");
/// var result = parser.Parse("doc-123", stream);
/// if (result.IsSuccess)
///     Console.WriteLine($"Parsed {result.PageCount} pages");
/// </code>
/// </example>
public ParseResult Parse(string documentId, Stream content) { ... }
```

Use `<inheritdoc/>` for interface implementations:

```csharp
/// <inheritdoc/>
public ParseResult Parse(string documentId, Stream content) { ... }
```

---

## What Does NOT Apply to Libraries

These sections from the shared AGENTS.md are **application-level concerns** — skip them for libraries:

- HTTP middleware, route guards, rate limiting
- Authentication / authorization / tenant isolation
- Health check endpoints (`/health`, `/ready`)
- Accessibility (a11y) / i18n / mobile responsiveness
- Frontend concerns (React, MSAL, UI components)
- `appsettings.json` / host configuration
- Database context / migrations (unless the library IS a data access library)

---

## Library Checklist (Pre-Merge)

- [ ] Multi-targets `net9.0;net10.0` (or current two supported versions)
- [ ] `PublicApiAnalyzers` tracks API surface — no unintentional breaking changes
- [ ] SemVer applied correctly (patch/minor/major per rules above)
- [ ] NuGet metadata complete (PackageId, Authors, Description, README, SourceLink)
- [ ] `ILogger<T>` via DI + `[LoggerMessage]` source generator — no sink configuration
- [ ] Named `ActivitySource` + `Meter` exposed as public constants with `.All` collection
- [ ] Error codes as constants — no HTTP status code awareness in library code
- [ ] All exceptions inherit from library base exception (`MyLibException`)
- [ ] FluentValidation rules have `.WithErrorCode()` on every rule
- [ ] `IOptions<T>` for configuration — library never reads files
- [ ] `AddMyLib()` extension method for DI registration
- [ ] Architecture tests verify no ASP.NET Core / hosting dependency
- [ ] XML docs on ALL public types, methods, properties
- [ ] BDD `.feature` files written before implementation
- [ ] 90%+ test coverage on new code (enforced in CI)
- [ ] `TreatWarningsAsErrors` enabled
- [ ] Deterministic builds enabled for CI
- [ ] README includes usage examples, telemetry registration, and error code reference
