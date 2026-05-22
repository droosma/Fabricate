# Copilot Instructions for Fabricate

## Build & Test

```bash
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~Fabricate.Tests.DiagnosticTests.FAB001_Emitted"  # single test
dotnet stryker --config-file stryker-config.json  # mutation testing
```

## Architecture

Fabricate is a **Roslyn incremental source generator** packaged as a single NuGet package (`netstandard2.0`). It has two generators:

- **`BuilderGenerator`** — Finds `[Fabricate<T>]` on partial classes, analyzes `T`'s properties/constructors, and emits builder code (fields, constructor, `With*`/`Without*` methods, `Build()`, implicit operator).
- **`FactoryGenerator`** — Collects all builders in the compilation and emits a `public static partial class A` (configurable) with factory properties.

The flow:  
`[Fabricate<T>]` → `TypeAnalyzer` inspects T → `NamingStrategy` decides method names → `WithMethodEmitter` + `BuildMethodEmitter` produce the source.

## Key Conventions

- **`ValidInstance()`**: Every builder must implement `static partial T ValidInstance()`. This defines the "known good" baseline. The generated constructor calls it to seed all fields.
- **With naming**: BCL types → `With{PropertyName}()`. Custom types (unique) → `With()`. Custom types (duplicated) → `With{PropertyName}()`.
- **Collections**: Always `params T[]` in the `With` method signature. Stored as arrays internally, converted to the target collection type in `Build()`.
- **Nullable**: Generates both `With{Property}(T?)` and `Without{Property}()`.
- **Constructor selection**: Pick the longest public constructor whose params all match properties by name+type. Fall back to parameterless.
- **Diagnostics**: FAB001 (error: missing ValidInstance), FAB002 (warning: naming), FAB003 (error: no constructor).
- **Testing**: 100% code coverage required. Stryker mutation testing at 100% threshold. Unit tests use Roslyn compilation testing (compile source → run generator → assert output).
