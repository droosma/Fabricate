using System.Collections.Immutable;
using FluentAssertions;
using Xunit;

namespace Fabricate.Tests;

public class TypeAnalyzerCoverageTests
{
    [Fact]
    public void GetAllProperties_SkipsStaticAndIndexerProperties()
    {
        var source = """
namespace TestApp;

public class BaseType
{
    public string Name { get; set; } = string.Empty;
}

public class DerivedType : BaseType
{
    public static string GlobalName { get; set; } = string.Empty;
    public int this[int index]
    {
        get => index;
        set { }
    }

    public new string Name { get; set; } = string.Empty;
    public int Age { get; init; }
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.DerivedType");

        var properties = TypeAnalyzer.GetAllProperties(typeSymbol);

        properties.Select(property => property.Name).Should().BeEquivalentTo("Name", "Age");
    }

    [Fact]
    public void SelectConstructor_ReturnsNull_WhenNoPublicConstructorsExist()
    {
        var source = """
namespace TestApp;

public class HiddenConstructorType
{
    public string Name { get; } = string.Empty;

    private HiddenConstructorType()
    {
    }
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.HiddenConstructorType");
        var properties = TypeAnalyzer.GetAllProperties(typeSymbol);

        TypeAnalyzer.SelectConstructor(typeSymbol, properties).Should().BeNull();
    }

    [Fact]
    public void SelectConstructor_FallsBackToParameterlessConstructor_WhenLongestConstructorDoesNotMatch()
    {
        var source = """
namespace TestApp;

public class MixedConstructors
{
    public string Name { get; set; } = string.Empty;

    public MixedConstructors(int age)
    {
    }

    public MixedConstructors()
    {
    }
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.MixedConstructors");

        var constructor = TypeAnalyzer.SelectConstructor(typeSymbol, ImmutableArray<PropertyInfo>.Empty);

        constructor.Should().NotBeNull();
        constructor!.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void SelectConstructor_ReturnsNull_WhenNoConstructorMatchesAndNoParameterlessExists()
    {
        var source = """
namespace TestApp;

public class UnmatchedConstructors
{
    public string Name { get; set; } = string.Empty;

    public UnmatchedConstructors(int age)
    {
    }
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.UnmatchedConstructors");
        var properties = TypeAnalyzer.GetAllProperties(typeSymbol);

        TypeAnalyzer.SelectConstructor(typeSymbol, properties).Should().BeNull();
    }

    [Fact]
    public void IsCollectionType_RecognizesArrays_AndRejectsTypeParameters()
    {
        var source = """
namespace TestApp;

public class Container<T>
{
    public T Value { get; set; } = default!;
    public int[] Numbers { get; set; } = System.Array.Empty<int>();
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.Container`1");

        var valueType = TestHarness.GetPropertySymbol(typeSymbol, "Value").Type;
        var numbersType = TestHarness.GetPropertySymbol(typeSymbol, "Numbers").Type;

        TypeAnalyzer.IsCollectionType(numbersType).Should().BeTrue();
        TypeAnalyzer.IsCollectionType(valueType).Should().BeFalse();
    }

    [Fact]
    public void GetCollectionElementType_ReturnsArrayElementType_AndNullForNonGenericTypes()
    {
        var source = """
namespace TestApp;

public class Container
{
    public int[] Numbers { get; set; } = System.Array.Empty<int>();
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.Container");
        var numbersType = TestHarness.GetPropertySymbol(typeSymbol, "Numbers").Type;
        var stringType = TestHarness.GetTypeSymbol(compilation, "System.String");

        TypeAnalyzer.GetCollectionElementType(numbersType)!.ToDisplayString().Should().Be("int");
        TypeAnalyzer.GetCollectionElementType(stringType).Should().BeNull();
    }

    [Fact]
    public void ConstructorParameterInfo_ExposesParameterNameAndType()
    {
        var compilation = TestHarness.CreateCompilation(string.Empty);
        var intType = TestHarness.GetTypeSymbol(compilation, "System.Int32");
        var parameter = new ConstructorParameterInfo("age", intType, "Age");

        parameter.ParameterName.Should().Be("age");
        parameter.Type.Should().BeSameAs(intType);
        parameter.PropertyName.Should().Be("Age");
    }

    [Fact]
    public void ConstructorInfo_PreservesProvidedParameters()
    {
        var compilation = TestHarness.CreateCompilation(string.Empty);
        var intType = TestHarness.GetTypeSymbol(compilation, "System.Int32");
        var parameter = new ConstructorParameterInfo("age", intType, "Age");
        var constructor = new ConstructorInfo(ImmutableArray.Create(parameter));

        constructor.Parameters.Should().ContainSingle().Which.Should().BeSameAs(parameter);
    }
}
