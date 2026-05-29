using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Fabricate.Tests;

/// <summary>
/// Tests for the correctness fixes: collection materialization, nullable collections,
/// property filtering, ValidInstance accessibility, and factory property collisions.
/// </summary>
public class BugfixCoverageTests
{
    [Fact]
    public void GetAllProperties_ExcludesWriteOnlyProperties()
    {
        var source = """
namespace TestApp;

public class WriteOnlyHolder
{
    public string Name { get; set; } = string.Empty;
    public string Secret { set { } }
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.WriteOnlyHolder");

        var properties = TypeAnalyzer.GetAllProperties(typeSymbol);

        properties.Select(p => p.Name).Should().BeEquivalentTo("Name");
    }

    [Theory]
    [InlineData(Accessibility.Public, "public")]
    [InlineData(Accessibility.Internal, "internal")]
    [InlineData(Accessibility.Protected, "protected")]
    [InlineData(Accessibility.ProtectedOrInternal, "protected internal")]
    [InlineData(Accessibility.ProtectedAndInternal, "private protected")]
    [InlineData(Accessibility.Private, "private")]
    [InlineData(Accessibility.NotApplicable, "private")]
    public void AccessibilityKeyword_MapsEveryAccessibility(Accessibility accessibility, string expected)
    {
        NamingStrategy.AccessibilityKeyword(accessibility).Should().Be(expected);
    }

    [Fact]
    public void GetCollectionMaterialization_HandlesEveryCollectionShape()
    {
        var source = """
using System.Collections.Generic;
using System.Collections.Immutable;

namespace TestApp;

public class Holder
{
    public int[] Array { get; set; } = System.Array.Empty<int>();
    public List<string> List { get; set; } = new();
    public IReadOnlyList<string> Interface { get; set; } = new List<string>();
    public ImmutableArray<int> ImmutableConcrete { get; set; }
    public IImmutableList<int> ImmutableInterface { get; set; } = ImmutableList<int>.Empty;
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.Holder");

        string Materialize(string propertyName) =>
            TypeAnalyzer.GetCollectionMaterialization(
                TestHarness.GetPropertySymbol(typeSymbol, propertyName).Type, "_field");

        Materialize("Array").Should().Be("_field");
        Materialize("List").Should().Be("new global::System.Collections.Generic.List<string>(_field)");
        Materialize("Interface").Should().Be("new System.Collections.Generic.List<string>(_field)");
        Materialize("ImmutableConcrete").Should().Be("System.Collections.Immutable.ImmutableArray.CreateRange(_field)");
        Materialize("ImmutableInterface").Should().Be("System.Collections.Immutable.ImmutableList.CreateRange(_field)");
    }

    [Fact]
    public void IsMaterializableCollection_DistinguishesGenericFromNonGenericAndScalar()
    {
        var source = """
using System.Collections;
using System.Collections.Generic;

namespace TestApp;

public class Holder
{
    public int[] Array { get; set; } = System.Array.Empty<int>();
    public List<string> Generic { get; set; } = new();
    public IEnumerable NonGeneric { get; set; } = new List<string>();
    public int Scalar { get; set; }
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.Holder");

        bool IsMaterializable(string propertyName) =>
            TypeAnalyzer.IsMaterializableCollection(
                TestHarness.GetPropertySymbol(typeSymbol, propertyName).Type);

        IsMaterializable("Array").Should().BeTrue();
        IsMaterializable("Generic").Should().BeTrue();
        IsMaterializable("NonGeneric").Should().BeFalse();
        IsMaterializable("Scalar").Should().BeFalse();
    }

    [Fact]
    public void Build_MaterializesCollectionConstructorParameter()
    {
        var source = """
using System.Collections.Generic;
using Fabricate;

namespace TestApp;

public record Team(string Name, List<string> Members);

[Fabricate<Team>]
public partial class TeamBuilder
{
    private static partial Team ValidInstance() => new("Reds", new List<string> { "a" });
}
""";

        var generated = Generate(source, "TeamBuilder.g.cs");

        generated.Should().Contain("=> new global::TestApp.Team(_name, new global::System.Collections.Generic.List<string>(_members));");
    }

    [Fact]
    public void Build_MaterializesNullableCollectionConstructorParameter()
    {
        var source = """
using System.Collections.Generic;
using Fabricate;

namespace TestApp;

public record Team(string Name, List<string>? Members);

[Fabricate<Team>]
public partial class TeamBuilder
{
    private static partial Team ValidInstance() => new("Reds", null);
}
""";

        var generated = Generate(source, "TeamBuilder.g.cs");

        generated.Should().Contain("_members == null ? null : new global::System.Collections.Generic.List<string>(_members)");
    }

    [Fact]
    public void Build_MaterializesInterfaceArrayAndImmutableCollectionProperties()
    {
        var source = """
using System.Collections.Generic;
using System.Collections.Immutable;
using Fabricate;

namespace TestApp;

public class Shapes
{
    public IReadOnlyList<string> Tags { get; set; } = new List<string>();
    public int[] Scores { get; set; } = System.Array.Empty<int>();
    public ImmutableArray<int> Points { get; set; }
}

[Fabricate<Shapes>]
public partial class ShapesBuilder
{
    private static partial Shapes ValidInstance() => new()
    {
        Tags = new List<string> { "x" },
        Scores = new[] { 1 },
        Points = ImmutableArray.Create(1)
    };
}
""";

        var generated = Generate(source, "ShapesBuilder.g.cs");

        generated.Should().Contain("Tags = new System.Collections.Generic.List<string>(_tags),");
        generated.Should().Contain("Scores = _scores,");
        generated.Should().Contain("Points = System.Collections.Immutable.ImmutableArray.CreateRange(_points),");
    }

    [Fact]
    public void Build_GuardsNullableCollectionPropertyAgainstNull()
    {
        var source = """
using System.Collections.Generic;
using Fabricate;

namespace TestApp;

public class Holder
{
    public List<string>? Tags { get; set; }
}

[Fabricate<Holder>]
public partial class HolderBuilder
{
    private static partial Holder ValidInstance() => new() { Tags = null };
}
""";

        var generated = Generate(source, "HolderBuilder.g.cs");

        generated.Should().Contain("private string[]? _tags = default!;");
        generated.Should().Contain("_tags = seed.Tags == null ? null : System.Linq.Enumerable.ToArray(seed.Tags);");
        generated.Should().Contain("Tags = _tags == null ? null : new global::System.Collections.Generic.List<string>(_tags),");
        generated.Should().Contain("public HolderBuilder WithoutTags()");
    }

    [Fact]
    public void Builder_DoesNotEmitWithMethod_ForReadOnlyComputedProperty()
    {
        var source = """
using Fabricate;

namespace TestApp;

public class Profile
{
    public string First { get; set; } = string.Empty;
    public string Last { get; set; } = string.Empty;
    public string FullName => First + " " + Last;
}

[Fabricate<Profile>]
public partial class ProfileBuilder
{
    private static partial Profile ValidInstance() => new() { First = "Ada", Last = "Lovelace" };
}
""";

        var generated = Generate(source, "ProfileBuilder.g.cs");

        generated.Should().Contain("WithFirst(");
        generated.Should().Contain("WithLast(");
        generated.Should().NotContain("FullName");
        generated.Should().NotContain("_fullName");
    }

    [Fact]
    public void Builder_EmitsValidInstanceDeclaration_MatchingUserAccessibility()
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
    public static partial Patient ValidInstance() => new() { Name = "John" };
}
""";

        var generated = Generate(source, "PatientBuilder.g.cs");

        generated.Should().Contain("public static partial global::TestApp.Patient ValidInstance();");
        generated.Should().NotContain("private static partial global::TestApp.Patient ValidInstance();");
    }

    [Fact]
    public void FactoryGenerator_ReportsFAB004_AndEmitsOnlyFirst_OnPropertyCollision()
    {
        var source = """
using Fabricate;

namespace First { public class Money { public decimal Amount { get; set; } } }
namespace Second { public class Money { public decimal Amount { get; set; } } }

namespace Builders
{
    [Fabricate<First.Money>]
    public partial class FirstMoneyBuilder
    {
        private static partial First.Money ValidInstance() => new() { Amount = 1m };
    }

    [Fabricate<Second.Money>]
    public partial class SecondMoneyBuilder
    {
        private static partial Second.Money ValidInstance() => new() { Amount = 2m };
    }
}
""";

        var compilation = TestHarness.CreateCompilation(source);
        var result = TestHarness.RunGenerators(compilation, new BuilderGenerator(), new FactoryGenerator());
        var factory = TestHarness.GetGeneratedSources(result)["Builders.A.g.cs"];

        result.Diagnostics.Should().Contain(d => d.Id == "FAB004");
        System.Text.RegularExpressions.Regex.Matches(factory, "Money => new\\(\\);").Count.Should().Be(1);
    }

    private static string Generate(string source, string fileName)
    {
        var compilation = TestHarness.CreateCompilation(source);
        var result = TestHarness.RunGenerators(compilation, new BuilderGenerator());
        var sources = TestHarness.GetGeneratedSources(result);
        sources.Should().ContainKey(fileName);
        return sources[fileName];
    }
}
