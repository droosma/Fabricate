# Fabricate — LLM Handoff Document

## What Is This?

**Fabricate** is a .NET C# Source Generator NuGet package that generates builder pattern classes for test projects. Users annotate a partial class with `[Fabricate<T>]`, implement a `static partial T ValidInstance()` method, and the generator produces fluent `With`/`Without` methods plus a static factory class.

- **Repository:** https://github.com/droosma/Fabricate
- **NuGet:** https://www.nuget.org/packages/Fabricate
- **Owner:** Duncan Roosma (@droosma)
- **License:** MIT

---

## Project Structure

```
Fabricate/
├── src/Fabricate/                    # Source generator (netstandard2.0)
│   ├── BuilderGenerator.cs          # Main IIncrementalGenerator — orchestrates everything
│   ├── TypeAnalyzer.cs              # Extracts properties, constructors, collection detection
│   ├── NamingStrategy.cs            # With method naming (BCL vs custom, unique vs duplicate)
│   ├── WithMethodEmitter.cs         # Generates With/Without methods per property
│   ├── BuildMethodEmitter.cs        # Generates Build() with constructor + object initializer
│   ├── FactoryGenerator.cs          # Generates static factory class (e.g., `A.Patient`)
│   ├── DiagnosticDescriptors.cs     # FAB001, FAB002, FAB003, FAB004 diagnostics
│   ├── FabricateAttribute.cs        # [Fabricate<T>] attribute source
│   ├── FabricateFactoryAttribute.cs # [assembly: FabricateFactory("Name")] attribute
│   └── IsExternalInit.cs            # Polyfill for netstandard2.0 init properties
├── tests/
│   ├── Fabricate.Tests/             # 94 unit tests (xUnit + FluentAssertions)
│   └── Fabricate.IntegrationTests/  # 19 integration tests (real compilation scenarios)
├── .github/workflows/
│   ├── ci.yml                       # Build + test + coverage + mutation testing
│   └── release.yml                  # NuGet publish on GitHub release
├── Directory.Build.props            # Solution-wide: nullable, latest lang, warnings as errors
├── stryker-config.json              # Mutation testing config (break threshold: 100%)
└── Fabricate.sln
```

---

## Architecture & Design Decisions

### Source Generator Design

- **Incremental generator** (`IIncrementalGenerator`) — NOT the older `ISourceGenerator`. Uses syntax + semantic model providers for performance.
- **Target:** `netstandard2.0` — required for analyzer/generator compatibility with all .NET SDK versions.
- **Roslyn dependency:** `Microsoft.CodeAnalysis.CSharp 4.3.0` (minimum for incremental generators).
- The generator runs at compile time in the user's test project. It produces two outputs per builder: the builder partial class and a factory class entry.

### Key Behaviors

| Feature | Implementation |
|---------|---------------|
| Type detection | Classes, records, structs, record structs, readonly structs |
| Constructor selection | Prefers primary constructor, then largest public constructor |
| Property sources | Constructor params + settable/init properties (deduplicated) |
| BCL type naming | `WithPropertyName()` — type alone isn't descriptive |
| Custom type naming (unique) | `With(T value)` — type is self-describing |
| Custom type naming (duplicated) | `WithPropertyName()` — need disambiguation |
| Nullable properties | Generates both `WithX(T value)` and `WithoutX()` |
| Collections (IEnumerable/IList/etc.) | Generates `params` array: `WithItems(params T[] values)` |
| Factory class | Static `A` class (customizable via `[assembly: FabricateFactory("Name")]`) |
| Implicit operator | Builder implicitly converts to T (calls Build()) |

### Diagnostics

| ID | Severity | Trigger |
|----|----------|---------|
| FAB001 | Error | `ValidInstance()` not found or wrong signature |
| FAB002 | Warning | Builder class name doesn't end with "Builder" |
| FAB003 | Error | Cannot resolve any public constructor for target type |
| FAB004 | Warning | Duplicate factory property for the same target type (only first builder exposed) |

---

## Quality Gates (enforced in CI)

1. **100% line coverage** — CI fails if coverage drops below 100%
2. **100% mutation score** — Stryker threshold `break: 100` — CI fails if any mutant survives
3. **TreatWarningsAsErrors** — all projects treat warnings as errors

### Mutation Testing Notes

Source generators are tricky to mutation-test because they build strings via `StringBuilder.AppendLine()`. Key strategies used:

- **Golden-file tests** (`MutationKillingTests.cs`) assert COMPLETE generated output character-by-character. These kill most string/statement mutations.
- **Logic branch tests** (`MutationKillingLogicTests.cs`) verify conditional paths.
- **Targeted tests** (`RemainingMutationTests.cs`) cover edge cases in collection detection, nullable structs, type uniqueness.
- **3 equivalent mutants** are explicitly suppressed with `// Stryker disable once [MutatorType] : [reason]`:
  - `BuilderGenerator.cs` L25: unreachable null check after `.Where(x => x is not null)`
  - `BuilderGenerator.cs` L41: type argument guard (compiler enforces this)
  - `TypeAnalyzer.cs` L46: `First()` vs `FirstOrDefault()` on GroupBy results (groups can't be empty)

---

## CI/CD

### CI Pipeline (`.github/workflows/ci.yml`)

Runs on push/PR to `main`. Two jobs:

1. **build-and-test:** restore → build → test with coverage → assert 100% line coverage
2. **mutation-testing** (depends on build-and-test): install Stryker → run → upload HTML report → publish badge JSON to `badges` branch

Badge is a shields.io endpoint badge reading from `https://raw.githubusercontent.com/droosma/Fabricate/badges/.badges/mutation.json`.

### Release Pipeline (`.github/workflows/release.yml`)

Triggered on GitHub release publish:
1. Build → test → extract version from git tag (strips `v` prefix) → `dotnet pack` → push to NuGet.org

**To release:** Create a GitHub release with tag `v1.0.0` → package `1.0.0` is published to NuGet.

**Secret required:** `NUGET_API_KEY` (already configured in repo settings).

---

## Development Workflow

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run mutation testing locally
dotnet tool install -g dotnet-stryker   # one-time
dotnet stryker --config-file stryker-config.json
# HTML report: StrykerOutput/*/reports/mutation-report.html

# Pack locally
dotnet pack src/Fabricate/Fabricate.csproj -o ./nupkg
```

---

## NuGet Package Details

- **Package ID:** `Fabricate`
- **Current published version:** `0.0.1-alpha`
- **Development dependency:** Yes (`DevelopmentDependency=true`) — won't propagate to consumer's consumers
- **Source Link:** Enabled (Microsoft.SourceLink.GitHub) with embedded PDB for debug step-through
- **Package layout:** DLL goes in both `analyzers/dotnet/cs` (for generator) AND `lib/netstandard2.0` (for attribute resolution)

---

## Known Constraints & Gotchas

1. **netstandard2.0 limitations:** No `Span<T>`, no default interface implementations, must polyfill `IsExternalInit`.
2. **Line endings in tests:** Golden-file tests use `StringBuilder.AppendLine()` for both actual and expected — works cross-platform because both use same `Environment.NewLine`.
3. **Stryker `mutate` filter:** Do NOT add a `"mutate"` section to `stryker-config.json` — the glob is relative to project internals and previously caused all mutants to be silently excluded.
4. **Generator is not unit-testable in isolation** — it requires Roslyn compilation infrastructure. Unit tests use `CSharpGeneratorDriver` + in-memory compilation via `Microsoft.CodeAnalysis.CSharp`.
5. **Package version in .csproj** (`0.1.0`) is overridden at pack time by `-p:Version=` from the release workflow's git tag.

---

## Potential Future Work

- Release stable `1.0.0` (user wants to manually test first)
- Support for `required` properties (C# 11)
- Support for generic target types
- Custom default value strategies beyond `ValidInstance()`
- Analyzer to warn if `ValidInstance()` returns null for non-nullable properties
- Consider moving to .NET 8 minimum if netstandard2.0 becomes too limiting

---

## Test Organization

| File | Purpose | Count |
|------|---------|-------|
| `BuilderGeneratorTests.cs` | Core generation: classes, records, structs, constructors | ~30 |
| `FactoryGeneratorTests.cs` | Factory class generation, custom names | ~10 |
| `DiagnosticTests.cs` | FAB001, FAB002, FAB003 emission | ~8 |
| `MutationKillingTests.cs` | Golden-file: full output assertion | ~12 |
| `MutationKillingLogicTests.cs` | Logic branches: wrong signatures, namespaces | ~15 |
| `RemainingMutationTests.cs` | Edge cases: nullable structs, collections, uniqueness | ~19 |
| `Fabricate.IntegrationTests/` | End-to-end: real `[Fabricate<T>]` usage with builders | 19 |

---

## Summary for AI Context

When working on this codebase:
- The generator produces C# source code as strings — changes to emitters affect generated output
- Any source change MUST maintain 100% mutation score (run Stryker locally to verify)
- Golden-file tests will break if you change ANY whitespace/formatting in emitted code — update them
- The test infrastructure uses `CSharpGeneratorDriver.Create()` with real Roslyn compilation
- Integration tests actually consume the generator (project references `src/Fabricate`)
- All CI is green as of the last commit; badges work correctly
