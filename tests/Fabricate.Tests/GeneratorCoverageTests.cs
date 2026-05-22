using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Fabricate.Tests;

public class GeneratorCoverageTests
{
    [Fact]
    public void BuilderGenerator_ReportsFAB003_WhenConstructorCannotBeResolved()
    {
        var source = """
using Fabricate;

namespace TestApp;

public class Patient
{
    public string Name { get; } = string.Empty;

    public Patient(int age)
    {
    }
}

[Fabricate<Patient>]
public partial class PatientBuilder
{
    private static partial Patient ValidInstance() => new(42);
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var result = TestHarness.RunGenerators(compilation, new BuilderGenerator());

        result.Diagnostics.Should().Contain(d => d.Id == "FAB003");
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void BuilderGenerator_HandlesNonGenericCollections()
    {
        var source = """
using System.Collections;
using System.Collections.Generic;
using Fabricate;

namespace TestApp;

public class Basket
{
    public IEnumerable Items { get; set; } = new List<string> { "apple" };
}

[Fabricate<Basket>]
public partial class BasketBuilder
{
    private static partial Basket ValidInstance() => new();
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var result = TestHarness.RunGenerators(compilation, new BuilderGenerator());
        var generated = TestHarness.GetGeneratedSources(result)["BasketBuilder.g.cs"];

        generated.Should().Contain("private global::System.Collections.IEnumerable _items = default!;");
        generated.Should().Contain("_items = seed.Items;");
        generated.Should().Contain("public BasketBuilder WithItems(global::System.Collections.IEnumerable items)");
    }

    [Fact]
    public void BuilderGenerator_EmitsBuildMethod_ForTypeWithoutProperties()
    {
        var source = """
using Fabricate;

namespace TestApp;

public class EmptyThing
{
}

[Fabricate<EmptyThing>]
public partial class EmptyThingBuilder
{
    private static partial EmptyThing ValidInstance() => new();
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var result = TestHarness.RunGenerators(compilation, new BuilderGenerator());
        var generated = TestHarness.GetGeneratedSources(result)["EmptyThingBuilder.g.cs"];

        generated.Should().Contain("public global::TestApp.EmptyThing Build() => new global::TestApp.EmptyThing();");
    }

    [Fact]
    public void FactoryGenerator_UsesDefaultName_WhenAssemblyAttributeOmitsArgument()
    {
        var source = """
using Fabricate;

[assembly: FabricateFactory]

namespace TestApp;

public class Patient
{
    public string Name { get; set; } = string.Empty;
}

[Fabricate<Patient>]
public partial class PatientBuilder
{
    private static partial Patient ValidInstance() => new();
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var result = TestHarness.RunGenerators(compilation, new BuilderGenerator(), new FactoryGenerator());
        var generated = TestHarness.GetGeneratedSources(result);

        generated.Should().ContainKey("TestApp.A.g.cs");
        generated["TestApp.A.g.cs"].Should().Contain("public static partial class A");
    }

    [Fact]
    public void FactoryGenerator_DoesNotGenerateFactory_WhenNoBuildersExist()
    {
        var source = """
using Fabricate;

[assembly: FabricateFactory]

namespace TestApp;

public class Patient
{
    public string Name { get; set; } = string.Empty;
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var result = TestHarness.RunGenerators(compilation, new FactoryGenerator());

        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void BuilderGenerator_GetBuilderInfo_ReturnsNull_WhenTargetSymbolIsNotANamedType()
    {
        var source = """
namespace TestApp;

public class Example
{
    public string Name { get; set; } = string.Empty;
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var context = TestHarness.CreatePropertyContext(compilation, "TestApp.Example", "Name");

        var result = TestHarness.InvokePrivateStatic(typeof(BuilderGenerator), "GetBuilderInfo", context);

        result.Should().BeNull();
    }

    [Fact]
    public void BuilderGenerator_GetBuilderInfo_ReturnsNull_WhenAttributesAreMissing()
    {
        var source = """
namespace TestApp;

public class Example
{
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var context = TestHarness.CreateTypeContext(compilation, "TestApp.Example", ImmutableArray<AttributeData>.Empty);

        var result = TestHarness.InvokePrivateStatic(typeof(BuilderGenerator), "GetBuilderInfo", context);

        result.Should().BeNull();
    }

    [Fact]
    public void BuilderGenerator_GetBuilderInfo_ReturnsNull_ForNonGenericAttribute()
    {
        var source = """
namespace TestApp;

[System.Obsolete]
public class Example
{
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.Example");
        var attribute = TestHarness.GetAttribute(typeSymbol, "System.ObsoleteAttribute");
        var context = TestHarness.CreateTypeContext(compilation, "TestApp.Example", ImmutableArray.Create(attribute));

        var result = TestHarness.InvokePrivateStatic(typeof(BuilderGenerator), "GetBuilderInfo", context);

        result.Should().BeNull();
    }

    [Fact]
    public void BuilderGenerator_GetBuilderInfo_ReturnsNull_ForArrayTargetType()
    {
        var source = """
using Fabricate;

namespace TestApp;

[Fabricate<int[]>]
public partial class NumbersBuilder
{
    private static partial int[] ValidInstance() => System.Array.Empty<int>();
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.NumbersBuilder");
        var attribute = TestHarness.GetAttribute(typeSymbol, "Fabricate.FabricateAttribute<T>");
        var context = TestHarness.CreateTypeContext(compilation, "TestApp.NumbersBuilder", ImmutableArray.Create(attribute));

        var result = TestHarness.InvokePrivateStatic(typeof(BuilderGenerator), "GetBuilderInfo", context);

        result.Should().BeNull();
    }

    [Fact]
    public void FactoryGenerator_GetFactoryEntry_ReturnsNull_WhenTargetSymbolIsNotANamedType()
    {
        var source = """
namespace TestApp;

public class Example
{
    public string Name { get; set; } = string.Empty;
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var context = TestHarness.CreatePropertyContext(compilation, "TestApp.Example", "Name");

        var result = TestHarness.InvokePrivateStatic(typeof(FactoryGenerator), "GetFactoryEntry", context);

        result.Should().BeNull();
    }

    [Fact]
    public void FactoryGenerator_GetFactoryEntry_ReturnsNull_ForNonGenericAttribute()
    {
        var source = """
namespace TestApp;

[System.Obsolete]
public class Example
{
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.Example");
        var attribute = TestHarness.GetAttribute(typeSymbol, "System.ObsoleteAttribute");
        var context = TestHarness.CreateTypeContext(compilation, "TestApp.Example", ImmutableArray.Create(attribute));

        var result = TestHarness.InvokePrivateStatic(typeof(FactoryGenerator), "GetFactoryEntry", context);

        result.Should().BeNull();
    }

    [Fact]
    public void FactoryGenerator_GetFactoryEntry_ReturnsNull_ForArrayTargetType()
    {
        var source = """
using Fabricate;

namespace TestApp;

[Fabricate<int[]>]
public partial class NumbersBuilder
{
    private static partial int[] ValidInstance() => System.Array.Empty<int>();
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.NumbersBuilder");
        var attribute = TestHarness.GetAttribute(typeSymbol, "Fabricate.FabricateAttribute<T>");
        var context = TestHarness.CreateTypeContext(compilation, "TestApp.NumbersBuilder", ImmutableArray.Create(attribute));

        var result = TestHarness.InvokePrivateStatic(typeof(FactoryGenerator), "GetFactoryEntry", context);

        result.Should().BeNull();
    }

    [Fact]
    public void FactoryGenerator_GetFactoryName_ReturnsNull_WhenAttributesAreMissing()
    {
        var compilation = TestHarness.CreateCompilation(string.Empty);
        var context = TestHarness.CreateAssemblyContext(compilation, ImmutableArray<AttributeData>.Empty);

        var result = TestHarness.InvokePrivateStatic(typeof(FactoryGenerator), "GetFactoryName", context);

        result.Should().BeNull();
    }

    [Fact]
    public void FactoryGenerator_GetFactoryName_ReturnsDefaultValue_WhenAttributeHasNoArguments()
    {
        var source = """
using System;

[assembly: Obsolete]
""";

        var compilation = TestHarness.CreateCompilation(source);
        var attribute = TestHarness.GetAttribute(compilation.Assembly, "System.ObsoleteAttribute");
        var context = TestHarness.CreateAssemblyContext(compilation, ImmutableArray.Create(attribute));

        var result = TestHarness.InvokePrivateStatic(typeof(FactoryGenerator), "GetFactoryName", context);

        result.Should().Be("A");
    }

    [Fact]
    public void FactoryEntry_ExposesBuilderName()
    {
        var entry = TestHarness.CreateInternalInstance(
            "Fabricate.FactoryEntry",
            "global::TestApp.PatientBuilder",
            "PatientBuilder",
            "Patient",
            "TestApp");

        entry.GetType().GetProperty("BuilderName")!.GetValue(entry).Should().Be("PatientBuilder");
    }
}
