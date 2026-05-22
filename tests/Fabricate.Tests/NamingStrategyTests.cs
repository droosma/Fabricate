using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using FluentAssertions;

namespace Fabricate.Tests;

public class NamingStrategyTests
{
    [Theory]
    [InlineData("System.String", true)]
    [InlineData("System.Int32", true)]
    [InlineData("System.DateTime", true)]
    [InlineData("System.Guid", true)]
    [InlineData("System.Decimal", true)]
    [InlineData("Microsoft.Extensions.Logging.ILogger", true)]
    public async Task IsBclType_ReturnsTrue_ForSystemTypes(string typeName, bool expected)
    {
        var type = await GetTypeSymbol(typeName);
        if (type != null)
        {
            NamingStrategy.IsBclType(type).Should().Be(expected);
        }
    }

    [Fact]
    public async Task IsBclType_ReturnsFalse_ForCustomTypes()
    {
        var source = @"
namespace TestApp;
public class Address { public string Street { get; set; } = """"; }
";
        var compilation = CreateCompilation(source);
        var type = compilation.GetTypeByMetadataName("TestApp.Address");
        type.Should().NotBeNull();
        NamingStrategy.IsBclType(type!).Should().BeFalse();
    }

    [Fact]
    public void GetFieldName_ConvertsPropertyName_ToCamelCaseWithUnderscore()
    {
        NamingStrategy.GetFieldName("Name").Should().Be("_name");
        NamingStrategy.GetFieldName("DateOfBirth").Should().Be("_dateOfBirth");
        NamingStrategy.GetFieldName("HomeAddress").Should().Be("_homeAddress");
    }

    private static async Task<ITypeSymbol?> GetTypeSymbol(string fullyQualifiedName)
    {
        var compilation = CreateCompilation("");
        return compilation.GetTypeByMetadataName(fullyQualifiedName);
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
