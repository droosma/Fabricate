using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using FluentAssertions;

namespace Fabricate.Tests;

public class FactoryGeneratorTests
{
    [Fact]
    public async Task FactoryGenerator_CreatesClassA_WithBuilderProperties()
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

        var generatedSources = await RunGenerator(source);
        
        var factorySource = generatedSources.Values.FirstOrDefault(v => v.Contains("partial class A"));
        factorySource.Should().NotBeNull();
        factorySource.Should().Contain("Patient => new()");
    }

    [Fact]
    public async Task FactoryGenerator_UsesCustomName_FromAttribute()
    {
        var source = @"
using Fabricate;

[assembly: FabricateFactory(""Given"")]

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

        var generatedSources = await RunGenerator(source);
        
        var factorySource = generatedSources.Values.FirstOrDefault(v => v.Contains("partial class Given"));
        factorySource.Should().NotBeNull();
    }

    private static async Task<Dictionary<string, string>> RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        
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

        var generators = new IIncrementalGenerator[] { new BuilderGenerator(), new FactoryGenerator() };
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generators.Select(g => g.AsSourceGenerator()).ToArray());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        var result = driver.GetRunResult();
        
        return result.GeneratedTrees
            .ToDictionary(
                t => Path.GetFileName(t.FilePath),
                t => t.GetText().ToString());
    }
}
