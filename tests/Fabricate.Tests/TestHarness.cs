using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Fabricate.Tests;

internal static class TestHarness
{
    public static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(FabricateAttribute<>).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(FabricateFactoryAttribute).Assembly.Location));

        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));
    }

    public static GeneratorDriverRunResult RunGenerators(CSharpCompilation compilation, params IIncrementalGenerator[] generators)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generators.Select(g => g.AsSourceGenerator()).ToArray());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        return driver.GetRunResult();
    }

    public static Dictionary<string, string> GetGeneratedSources(GeneratorDriverRunResult result)
    {
        return result.GeneratedTrees.ToDictionary(
            tree => Path.GetFileName(tree.FilePath),
            tree => tree.GetText().ToString());
    }

    public static INamedTypeSymbol GetTypeSymbol(CSharpCompilation compilation, string metadataName)
    {
        return compilation.GetTypeByMetadataName(metadataName)
            ?? throw new InvalidOperationException($"Could not find type '{metadataName}'.");
    }

    public static IPropertySymbol GetPropertySymbol(INamedTypeSymbol typeSymbol, string propertyName)
    {
        return typeSymbol.GetMembers(propertyName)
            .OfType<IPropertySymbol>()
            .Single();
    }

    public static AttributeData GetAttribute(ISymbol symbol, string attributeMetadataName)
    {
        return symbol.GetAttributes().Single(attribute =>
            attribute.AttributeClass?.ToDisplayString() == attributeMetadataName ||
            attribute.AttributeClass?.OriginalDefinition.ToDisplayString() == attributeMetadataName);
    }

    public static GeneratorAttributeSyntaxContext CreateTypeContext(
        CSharpCompilation compilation,
        string metadataName,
        ImmutableArray<AttributeData> attributes)
    {
        var typeSymbol = GetTypeSymbol(compilation, metadataName);
        var syntax = (TypeDeclarationSyntax)typeSymbol.DeclaringSyntaxReferences.Single().GetSyntax();
        return CreateContext(compilation, syntax, typeSymbol, attributes);
    }

    public static GeneratorAttributeSyntaxContext CreatePropertyContext(
        CSharpCompilation compilation,
        string metadataName,
        string propertyName)
    {
        var typeSymbol = GetTypeSymbol(compilation, metadataName);
        var propertySymbol = GetPropertySymbol(typeSymbol, propertyName);
        var syntax = (PropertyDeclarationSyntax)propertySymbol.DeclaringSyntaxReferences.Single().GetSyntax();
        return CreateContext(compilation, syntax, propertySymbol, ImmutableArray<AttributeData>.Empty);
    }

    public static GeneratorAttributeSyntaxContext CreateAssemblyContext(
        CSharpCompilation compilation,
        ImmutableArray<AttributeData> attributes)
    {
        var syntax = (CompilationUnitSyntax)compilation.SyntaxTrees.Single().GetRoot();
        return CreateContext(compilation, syntax, compilation.Assembly, attributes);
    }

    public static object? InvokePrivateStatic(Type type, string methodName, params object[] arguments)
    {
        var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Could not find method '{methodName}'.");
        return method.Invoke(null, arguments);
    }

    public static object CreateInternalInstance(string typeName, params object?[] arguments)
    {
        var type = typeof(BuilderGenerator).Assembly.GetType(typeName, throwOnError: true)
            ?? throw new InvalidOperationException($"Could not find type '{typeName}'.");

        return Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: arguments,
            culture: null)
            ?? throw new InvalidOperationException($"Could not create instance of '{typeName}'.");
    }

    private static GeneratorAttributeSyntaxContext CreateContext(
        CSharpCompilation compilation,
        SyntaxNode targetNode,
        ISymbol targetSymbol,
        ImmutableArray<AttributeData> attributes)
    {
        var semanticModel = compilation.GetSemanticModel(targetNode.SyntaxTree);
        var constructor = typeof(GeneratorAttributeSyntaxContext)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single();

        return (GeneratorAttributeSyntaxContext)constructor.Invoke(new object[]
        {
            targetNode,
            targetSymbol,
            semanticModel,
            attributes
        });
    }
}
