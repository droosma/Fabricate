using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using FluentAssertions;
using Xunit;

namespace Fabricate.Tests;

public class EdgeCaseTests
{
    [Fact]
    public async Task Generator_HandlesEmptyClass()
    {
        var source = @"
using Fabricate;

namespace TestApp;

public class EmptyMarker { }

[Fabricate<EmptyMarker>]
public partial class EmptyMarkerBuilder
{
    private static partial EmptyMarker ValidInstance() => new();
}
";

        var generatedSources = await RunBuilderGenerator(source);
        generatedSources.Should().ContainKey("EmptyMarkerBuilder.g.cs");

        var generated = generatedSources["EmptyMarkerBuilder.g.cs"];
        generated.Should().Contain("partial class EmptyMarkerBuilder");
        generated.Should().Contain("Build()");
        generated.Should().Contain("implicit operator");
        generated.Should().NotContain("public EmptyMarkerBuilder With");
        generated.Should().NotContain("public EmptyMarkerBuilder Without");
    }

    [Fact]
    public async Task Generator_HandlesConstructorOnlyProperties()
    {
        var source = @"
using Fabricate;

namespace TestApp;

public class Immutable
{
    public string Value { get; }
    public Immutable(string value) { Value = value; }
}

[Fabricate<Immutable>]
public partial class ImmutableBuilder
{
    private static partial Immutable ValidInstance() => new(""seed"");
}
";

        var generatedSources = await RunBuilderGenerator(source);
        var generated = generatedSources["ImmutableBuilder.g.cs"];
        generated.Should().Contain("public ImmutableBuilder WithValue(");
        generated.Should().Contain("=> new global::TestApp.Immutable(_value);");
    }

    [Fact]
    public async Task Generator_TreatsInitOnlyPropertiesAsSettable()
    {
        var source = @"
using Fabricate;

namespace TestApp;

public class InitOnly
{
    public string Name { get; init; } = """";
    public int Age { get; init; }
}

[Fabricate<InitOnly>]
public partial class InitOnlyBuilder
{
    private static partial InitOnly ValidInstance() => new() { Name = ""John"", Age = 30 };
}
";

        var generatedSources = await RunBuilderGenerator(source);
        var generated = generatedSources["InitOnlyBuilder.g.cs"];
        generated.Should().Contain("public InitOnlyBuilder WithName(");
        generated.Should().Contain("public InitOnlyBuilder WithAge(");
        generated.Should().Contain("Name = _name,");
        generated.Should().Contain("Age = _age,");
    }

    [Fact]
    public async Task Generator_UsesCorrectDeepNamespace()
    {
        var source = @"
using Fabricate;

namespace Very.Deep.Nested.Namespace;

public class DeepType
{
    public string Value { get; set; } = """";
}

[Fabricate<DeepType>]
public partial class DeepTypeBuilder
{
    private static partial DeepType ValidInstance() => new() { Value = ""seed"" };
}
";

        var generatedSources = await RunBuilderGenerator(source);
        var generated = generatedSources["DeepTypeBuilder.g.cs"];
        generated.Should().Contain("namespace Very.Deep.Nested.Namespace;");
        generated.Should().Contain("partial class DeepTypeBuilder");
    }

    [Fact]
    public async Task FactoryGenerator_IncludesMultipleBuildersInSameCompilation()
    {
        var source = @"
using Fabricate;

namespace TestApp;

public class Patient
{
    public string Name { get; set; } = """";
}

public class Appointment
{
    public string Title { get; set; } = """";
}

[Fabricate<Patient>]
public partial class PatientBuilder
{
    private static partial Patient ValidInstance() => new() { Name = ""John"" };
}

[Fabricate<Appointment>]
public partial class AppointmentBuilder
{
    private static partial Appointment ValidInstance() => new() { Title = ""Checkup"" };
}
";

        var generatedSources = await RunAllGenerators(source);
        var factorySource = generatedSources.Values.Single(v => v.Contains("public static partial class A"));
        factorySource.Should().Contain("Patient => new();");
        factorySource.Should().Contain("Appointment => new();");
    }

    [Fact]
    public async Task Generator_UsesPropertyNamesForDuplicateBclTypes()
    {
        var source = @"
using Fabricate;

namespace TestApp;

public class TwoStrings
{
    public string FirstName { get; set; } = """";
    public string LastName { get; set; } = """";
}

[Fabricate<TwoStrings>]
public partial class TwoStringsBuilder
{
    private static partial TwoStrings ValidInstance() => new() { FirstName = ""Ada"", LastName = ""Lovelace"" };
}
";

        var generatedSources = await RunBuilderGenerator(source);
        var generated = generatedSources["TwoStringsBuilder.g.cs"];
        generated.Should().Contain("WithFirstName(");
        generated.Should().Contain("WithLastName(");
        generated.Should().NotContain("public TwoStringsBuilder With(");
    }

    private static Task<Dictionary<string, string>> RunBuilderGenerator(string source)
        => RunGenerators(source, new BuilderGenerator());

    private static Task<Dictionary<string, string>> RunAllGenerators(string source)
        => RunGenerators(source, new BuilderGenerator(), new FactoryGenerator());

    private static Task<Dictionary<string, string>> RunGenerators(string source, params IIncrementalGenerator[] generators)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(FabricateAttribute<>).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(FabricateFactoryAttribute).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators.Select(g => g.AsSourceGenerator()).ToArray(),
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var errors = diagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        errors.Should().BeEmpty();

        var result = driver.GetRunResult();
        var generatedSources = result.GeneratedTrees
            .ToDictionary(
                t => Path.GetFileName(t.FilePath),
                t => t.GetText().ToString());

        return Task.FromResult(generatedSources);
    }
}
