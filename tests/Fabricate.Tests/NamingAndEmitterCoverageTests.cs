using System.Collections.Immutable;
using FluentAssertions;
using Xunit;

namespace Fabricate.Tests;

public class NamingAndEmitterCoverageTests
{
    [Fact]
    public void NamingStrategy_IsBclType_UnwrapsNullableValueTypes()
    {
        var source = """
namespace TestApp;

public class NullableHolder
{
    public int? Age { get; set; }
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.NullableHolder");
        var propertyType = TestHarness.GetPropertySymbol(typeSymbol, "Age").Type;

        NamingStrategy.IsBclType(propertyType).Should().BeTrue();
    }

    [Fact]
    public void NamingStrategy_GetWithMethodName_UnwrapsNullableValueTypes()
    {
        var source = """
namespace TestApp;

public class NullableHolder
{
    public int? Age { get; set; }
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.NullableHolder");
        var propertyType = TestHarness.GetPropertySymbol(typeSymbol, "Age").Type;
        var property = new PropertyInfo("Age", propertyType, isSettable: true, isNullable: true);

        NamingStrategy.GetWithMethodName(property, isTypeUnique: true).Should().Be("WithAge");
    }

    [Fact]
    public void WithMethodEmitter_HandlesCollectionElementTypes_NonGenericCollections_AndNullableValueTypes()
    {
        var source = """
using System.Collections;
using System.Collections.Generic;

namespace TestApp;

public class Dependency
{
}

public class Example
{
    public List<Dependency> Dependencies { get; set; } = new();
    public IEnumerable Items { get; set; } = new ArrayList();
    public int? Age { get; set; }
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.Example");
        var dependencies = TestHarness.GetPropertySymbol(typeSymbol, "Dependencies");
        var items = TestHarness.GetPropertySymbol(typeSymbol, "Items");
        var age = TestHarness.GetPropertySymbol(typeSymbol, "Age");
        var properties = ImmutableArray.Create(
            new PropertyInfo(dependencies.Name, dependencies.Type, isSettable: true, isNullable: false),
            new PropertyInfo(items.Name, items.Type, isSettable: true, isNullable: false),
            new PropertyInfo(age.Name, age.Type, isSettable: true, isNullable: true));

        var emitted = WithMethodEmitter.Emit("ExampleBuilder", properties);

        emitted.Should().Contain("public ExampleBuilder With(params global::TestApp.Dependency[] dependencies)");
        emitted.Should().Contain("public ExampleBuilder WithItems(global::System.Collections.IEnumerable items)");
        emitted.Should().Contain("public ExampleBuilder WithAge(int? age)");
        emitted.Should().Contain("public ExampleBuilder WithoutAge()");
    }

    [Fact]
    public void BuildMethodEmitter_EmitsSimpleConstruction_WhenNoPropertiesNeedInitialization()
    {
        var compilation = TestHarness.CreateCompilation("namespace TestApp; public class EmptyThing { }");
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.EmptyThing");
        var constructor = new ConstructorInfo(ImmutableArray<ConstructorParameterInfo>.Empty);

        var emitted = BuildMethodEmitter.Emit(typeSymbol, constructor, ImmutableArray<PropertyInfo>.Empty);

        emitted.Should().Be($"    public global::TestApp.EmptyThing Build() => new global::TestApp.EmptyThing();{Environment.NewLine}");
    }
}
