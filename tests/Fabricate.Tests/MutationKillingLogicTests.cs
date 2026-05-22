using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Fabricate.Tests;

public class MutationKillingLogicTests
{
    [Fact]
    public void BuilderGenerator_GetBuilderInfo_ReturnsNull_ForGenericAttributeWithWrongArity()
    {
        var source = """
namespace TestApp;

[System.AttributeUsage(System.AttributeTargets.Class)]
public sealed class DualFabricateAttribute<TTarget, TOther> : System.Attribute
{
}

public class Thing
{
}

[DualFabricate<Thing, string>]
public partial class ThingBuilder
{
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var builderSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.ThingBuilder");
        var attribute = builderSymbol.GetAttributes().Single();
        var context = TestHarness.CreateTypeContext(compilation, "TestApp.ThingBuilder", ImmutableArray.Create(attribute));

        var result = TestHarness.InvokePrivateStatic(typeof(BuilderGenerator), "GetBuilderInfo", context);

        result.Should().BeNull();
    }

    [Fact]
    public void BuilderGenerator_ReportsFAB001_WhenValidInstanceIsNonStatic()
    {
        var source = """
using Fabricate;

namespace TestApp;

public class Patient
{
    public string Name { get; set; } = string.Empty;
}

[Fabricate<Patient>]
public partial class PatientBuilder
{
    private partial Patient ValidInstance() => new() { Name = "John" };
}
""";

        var result = RunGenerators(source, new BuilderGenerator());
        var generated = TestHarness.GetGeneratedSources(result)["PatientBuilder.g.cs"];

        result.Diagnostics.Should().Contain(d => d.Id == "FAB001");
        result.Diagnostics.Should().NotContain(d => d.Id == "FAB003");
        generated.Should().Contain("private static partial global::TestApp.Patient ValidInstance();");
        generated.Should().NotContain("private global::TestApp.Patient Build() => new");
    }

    [Fact]
    public void BuilderGenerator_ReportsFAB001_WhenValidInstanceHasParameters()
    {
        var source = """
using Fabricate;

namespace TestApp;

public class Patient
{
    public string Name { get; set; } = string.Empty;
}

[Fabricate<Patient>]
public partial class PatientBuilder
{
    private static partial Patient ValidInstance(int version) => new() { Name = version.ToString() };
}
""";

        var result = RunGenerators(source, new BuilderGenerator());
        var generated = TestHarness.GetGeneratedSources(result)["PatientBuilder.g.cs"];

        result.Diagnostics.Should().Contain(d => d.Id == "FAB001");
        result.Diagnostics.Should().NotContain(d => d.Id == "FAB003");
        generated.Should().Contain("private static partial global::TestApp.Patient ValidInstance();");
        generated.Should().NotContain("private global::TestApp.Patient Build() => new");
    }

    [Fact]
    public void BuilderGenerator_ReportsFAB001_WhenValidInstanceReturnsWrongType()
    {
        var source = """
using Fabricate;

namespace TestApp;

public class Patient
{
    public string Name { get; set; } = string.Empty;
}

[Fabricate<Patient>]
public partial class PatientBuilder
{
    private static partial string ValidInstance() => string.Empty;
}
""";

        var result = RunGenerators(source, new BuilderGenerator());
        var generated = TestHarness.GetGeneratedSources(result)["PatientBuilder.g.cs"];

        result.Diagnostics.Should().Contain(d => d.Id == "FAB001");
        result.Diagnostics.Should().NotContain(d => d.Id == "FAB003");
        generated.Should().Contain("private static partial global::TestApp.Patient ValidInstance();");
        generated.Should().NotContain("private global::TestApp.Patient Build() => new");
    }

    [Fact]
    public void BuilderGenerator_GlobalNamespaceBuilder_DoesNotEmitNamespaceWrapper()
    {
        var source = """
using Fabricate;

public class GlobalThing
{
    public string Name { get; set; } = string.Empty;
}

[Fabricate<GlobalThing>]
public partial class GlobalThingBuilder
{
    private static partial GlobalThing ValidInstance() => new() { Name = "global" };
}

namespace TestApp
{
    public class NamespacedThing
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fabricate<NamespacedThing>]
    public partial class NamespacedThingBuilder
    {
        private static partial NamespacedThing ValidInstance() => new() { Name = "scoped" };
    }
}
""";

        var generated = TestHarness.GetGeneratedSources(RunGenerators(source, new BuilderGenerator()));

        generated["GlobalThingBuilder.g.cs"].Should().Contain("partial class GlobalThingBuilder");
        generated["GlobalThingBuilder.g.cs"].Should().NotContain("namespace ");
        generated["NamespacedThingBuilder.g.cs"].Should().Contain("namespace TestApp;");
        generated["NamespacedThingBuilder.g.cs"].Should().NotContain("namespace ;");
    }

    [Fact]
    public void BuilderGenerator_DoesNotAppendQuestionMark_ToNullableValueTypeFields()
    {
        var source = """
#nullable enable
using Fabricate;

namespace TestApp;

public class Person
{
    public int? Age { get; set; }
    public string? Nickname { get; set; }
}

[Fabricate<Person>]
public partial class PersonBuilder
{
    private static partial Person ValidInstance() => new() { Age = 42, Nickname = null };
}
""";

        var generated = TestHarness.GetGeneratedSources(RunGenerators(source, new BuilderGenerator()))["PersonBuilder.g.cs"];

        generated.Should().Contain("private int? _age = default!;");
        generated.Should().NotContain("int?? _age");
        generated.Should().Contain("private string? _nickname = default!;");
        generated.Should().NotContain("string?? _nickname");
    }

    [Fact]
    public void FactoryGenerator_GetFactoryEntry_ReturnsNull_WhenAttributesAreMissing()
    {
        var source = """
namespace TestApp;

public class Thing
{
}

public partial class ThingBuilder
{
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var context = TestHarness.CreateTypeContext(compilation, "TestApp.ThingBuilder", ImmutableArray<AttributeData>.Empty);

        var result = TestHarness.InvokePrivateStatic(typeof(FactoryGenerator), "GetFactoryEntry", context);

        result.Should().BeNull();
    }

    [Fact]
    public void FactoryGenerator_GlobalNamespaceFactory_DoesNotEmitNamespaceWrapper()
    {
        var source = """
using Fabricate;

[assembly: FabricateFactory]

public class GlobalThing
{
    public string Name { get; set; } = string.Empty;
}

[Fabricate<GlobalThing>]
public partial class GlobalThingBuilder
{
    private static partial GlobalThing ValidInstance() => new() { Name = "global" };
}

namespace TestApp
{
    public class NamespacedThing
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fabricate<NamespacedThing>]
    public partial class NamespacedThingBuilder
    {
        private static partial NamespacedThing ValidInstance() => new() { Name = "scoped" };
    }
}
""";

        var generated = TestHarness.GetGeneratedSources(RunGenerators(source, new BuilderGenerator(), new FactoryGenerator()));

        generated["A.g.cs"].Should().Contain("public static partial class A");
        generated["A.g.cs"].Should().Contain("public static global::GlobalThingBuilder GlobalThing => new();");
        generated["A.g.cs"].Should().NotContain("namespace ");
        generated["TestApp.A.g.cs"].Should().Contain("namespace TestApp;");
        generated["TestApp.A.g.cs"].Should().Contain("public static global::TestApp.NamespacedThingBuilder NamespacedThing => new();");
        generated["TestApp.A.g.cs"].Should().NotContain("namespace ;");
    }

    [Fact]
    public void NamingStrategy_IsBclType_DifferentiatesSystemMicrosoftAndCustomNamespaces()
    {
        var source = """
namespace Microsoft.Contoso
{
    public class Widget
    {
    }
}

namespace TestApp
{
    public class Widget
    {
    }
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var systemType = TestHarness.GetTypeSymbol(compilation, "System.Uri");
        var microsoftType = TestHarness.GetTypeSymbol(compilation, "Microsoft.Contoso.Widget");
        var customType = TestHarness.GetTypeSymbol(compilation, "TestApp.Widget");

        NamingStrategy.IsBclType(systemType).Should().BeTrue();
        NamingStrategy.IsBclType(microsoftType).Should().BeTrue();
        NamingStrategy.IsBclType(customType).Should().BeFalse();
    }

    [Fact]
    public void NamingStrategy_GetWithMethodName_UsesBareWith_ForUniqueCustomTypes()
    {
        var source = """
namespace TestApp;

public class Dependency
{
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var dependencyType = TestHarness.GetTypeSymbol(compilation, "TestApp.Dependency");
        var stringType = TestHarness.GetTypeSymbol(compilation, "System.String");
        var customProperty = new PropertyInfo("Dependency", dependencyType, isSettable: true, isNullable: false);
        var nameProperty = new PropertyInfo("Name", stringType, isSettable: true, isNullable: false);

        NamingStrategy.GetWithMethodName(customProperty, isTypeUnique: true).Should().Be("With");
        NamingStrategy.GetWithMethodName(customProperty, isTypeUnique: true).Should().NotBe("WithDependency");
        NamingStrategy.GetWithMethodName(nameProperty, isTypeUnique: true).Should().Be("WithName");
        NamingStrategy.GetWithMethodName(nameProperty, isTypeUnique: true).Should().NotBe("With");
    }

    [Fact]
    public void BuilderGenerator_UsesCaseInsensitiveConstructorMatching_WhenEmittingBuildMethod()
    {
        var source = """
using Fabricate;

namespace TestApp;

public class Person
{
    public string Name { get; }
    public int Age { get; set; }

    public Person(string name)
    {
        Name = name;
    }
}

[Fabricate<Person>]
public partial class PersonBuilder
{
    private static partial Person ValidInstance() => new("John") { Age = 42 };
}
""";

        var generated = TestHarness.GetGeneratedSources(RunGenerators(source, new BuilderGenerator()))["PersonBuilder.g.cs"];

        generated.Should().Contain($"public global::TestApp.Person Build(){Environment.NewLine}        => new global::TestApp.Person(_name){Environment.NewLine}        {{");
        generated.Should().Contain("Age = _age,");
        generated.Should().NotContain("new global::TestApp.Person()");
        generated.Should().NotContain("Build()        => new");
        generated.Should().NotContain("new global::TestApp.Person(_name)        {");
    }

    [Fact]
    public void BuilderGenerator_ReportsFAB003_WhenConstructorParameterTypeDoesNotMatchPropertyType()
    {
        var source = """
using Fabricate;

namespace TestApp;

public class Person
{
    public string Name { get; }

    public Person(int name)
    {
        Name = name.ToString();
    }
}

[Fabricate<Person>]
public partial class PersonBuilder
{
    private static partial Person ValidInstance() => new(42);
}
""";

        var result = RunGenerators(source, new BuilderGenerator());

        result.Diagnostics.Should().Contain(d => d.Id == "FAB003");
        result.Diagnostics.Should().NotContain(d => d.Id == "FAB001");
        result.GeneratedTrees.Should().BeEmpty();
    }

    private static GeneratorDriverRunResult RunGenerators(string source, params IIncrementalGenerator[] generators)
    {
        var compilation = TestHarness.CreateCompilation(source);
        return TestHarness.RunGenerators(compilation, generators);
    }
}
