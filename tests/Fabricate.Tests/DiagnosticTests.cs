using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using FluentAssertions;

namespace Fabricate.Tests;

public class DiagnosticTests
{
    [Fact]
    public async Task FAB001_Emitted_WhenValidInstanceMissing()
    {
        var source = @"
using Fabricate;

namespace TestApp;

public class Patient
{
    public string Name { get; set; } = """";
}

[Fabricate<Patient>]
public partial class PatientBuilder;
";

        var diagnostics = await GetDiagnostics(source);
        diagnostics.Should().Contain(d => d.Id == "FAB001");
    }

    [Fact]
    public async Task FAB001_NotEmitted_WhenValidInstanceExists()
    {
        var source = @"
using Fabricate;

namespace TestApp;

public class Patient
{
    public string Name { get; set; } = """";
}

[Fabricate<Patient>]
public partial class PatientBuilder
{
    private static partial Patient ValidInstance() => new() { Name = ""John"" };
}
";

        var diagnostics = await GetDiagnostics(source);
        diagnostics.Should().NotContain(d => d.Id == "FAB001");
    }

    [Fact]
    public async Task FAB002_Emitted_WhenNameDoesNotEndWithBuilder()
    {
        var source = @"
using Fabricate;

namespace TestApp;

public class Patient
{
    public string Name { get; set; } = """";
}

[Fabricate<Patient>]
public partial class PatientFactory
{
    private static partial Patient ValidInstance() => new() { Name = ""John"" };
}
";

        var diagnostics = await GetDiagnostics(source);
        diagnostics.Should().Contain(d => d.Id == "FAB002");
    }

    [Fact]
    public async Task FAB002_NotEmitted_WhenNameEndsWithBuilder()
    {
        var source = @"
using Fabricate;

namespace TestApp;

public class Patient
{
    public string Name { get; set; } = """";
}

[Fabricate<Patient>]
public partial class PatientBuilder
{
    private static partial Patient ValidInstance() => new() { Name = ""John"" };
}
";

        var diagnostics = await GetDiagnostics(source);
        diagnostics.Should().NotContain(d => d.Id == "FAB002");
    }

    private static async Task<List<Diagnostic>> GetDiagnostics(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(FabricateAttribute<>).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var generator = new BuilderGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        var result = driver.GetRunResult();
        return result.Diagnostics.Concat(diagnostics).ToList();
    }
}
