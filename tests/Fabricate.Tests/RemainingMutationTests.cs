using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Fabricate.Tests;

/// <summary>
/// Tests targeting specific surviving mutations for 100% mutation score.
/// </summary>
public class RemainingMutationTests
{
    /// <summary>
    /// Kills NamingStrategy lines 17-19: nullable custom type unwrapping in IsBclType.
    /// Must use a VALUE TYPE (struct) since only value types become Nullable&lt;T&gt;.
    /// Reference type T? is just an annotation, not System.Nullable&lt;T&gt;.
    /// </summary>
    [Fact]
    public void IsBclType_ReturnsFalse_ForNullableCustomStruct()
    {
        var source = @"
namespace TestApp;
public struct Coordinate { public double X; public double Y; }
public class Holder { public Coordinate? Position { get; set; } }
";
        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.Holder");
        var propertyType = TestHarness.GetPropertySymbol(typeSymbol, "Position").Type;

        // Coordinate? is Nullable<Coordinate> - IsBclType should unwrap and check Coordinate (custom = false)
        NamingStrategy.IsBclType(propertyType).Should().BeFalse();
    }

    /// <summary>
    /// Kills NamingStrategy lines 44-46: nullable custom struct unwrapping in GetWithMethodName.
    /// </summary>
    [Fact]
    public void GetWithMethodName_UnwrapsNullableCustomStruct_ForUniqueCheck()
    {
        var source = @"
namespace TestApp;
public struct Coordinate { public double X; public double Y; }
public class Holder { public Coordinate? Position { get; set; } }
";
        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.Holder");
        var prop = TestHarness.GetPropertySymbol(typeSymbol, "Position");
        var propertyInfo = new PropertyInfo(prop.Name, prop.Type, isSettable: true, isNullable: true);

        // Nullable custom struct, unique → should be "With" not "WithPosition"
        NamingStrategy.GetWithMethodName(propertyInfo, isTypeUnique: true).Should().Be("With");
    }

    /// <summary>
    /// Kills NamingStrategy lines 44-46: non-unique nullable custom struct uses property name.
    /// </summary>
    [Fact]
    public void GetWithMethodName_UnwrapsNullableCustomStruct_NonUnique_UsesPropertyName()
    {
        var source = @"
namespace TestApp;
public struct Coordinate { public double X; public double Y; }
public class Holder { public Coordinate? Position { get; set; } }
";
        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.Holder");
        var prop = TestHarness.GetPropertySymbol(typeSymbol, "Position");
        var propertyInfo = new PropertyInfo(prop.Name, prop.Type, isSettable: true, isNullable: true);

        // Nullable custom struct, NOT unique → "WithPosition"
        NamingStrategy.GetWithMethodName(propertyInfo, isTypeUnique: false).Should().Be("WithPosition");
    }

    /// <summary>
    /// Kills TypeAnalyzer line 101: string mutation on "System.Collections.Immutable.".
    /// If mutated to empty string, ALL types would be detected as collections.
    /// This negative test ensures a non-collection type returns false.
    /// </summary>
    [Fact]
    public void IsCollectionType_ReturnsFalse_ForNonCollectionNamedType()
    {
        var source = @"
namespace TestApp;
public class NotACollection { public string Value { get; set; } = """"; }
public class Holder { public NotACollection Item { get; set; } = new(); }
";
        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.Holder");
        var propType = TestHarness.GetPropertySymbol(typeSymbol, "Item").Type;

        TypeAnalyzer.IsCollectionType(propType).Should().BeFalse();
    }

    /// <summary>
    /// Kills TypeAnalyzer line 102: string mutation on "System.Collections.IEnumerable".
    /// </summary>
    [Fact]
    public void IsCollectionType_DetectsIEnumerable()
    {
        var source = @"
using System.Collections;
namespace TestApp;
public class Holder { public IEnumerable Items { get; set; } = new System.Collections.ArrayList(); }
";
        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.Holder");
        var propType = TestHarness.GetPropertySymbol(typeSymbol, "Items").Type;

        TypeAnalyzer.IsCollectionType(propType).Should().BeTrue();
    }

    /// <summary>
    /// Kills TypeAnalyzer line 103: string mutation on "System.Collections.IList".
    /// </summary>
    [Fact]
    public void IsCollectionType_DetectsIList()
    {
        var source = @"
using System.Collections;
namespace TestApp;
public class Holder { public IList Items { get; set; } = new System.Collections.ArrayList(); }
";
        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.Holder");
        var propType = TestHarness.GetPropertySymbol(typeSymbol, "Items").Type;

        TypeAnalyzer.IsCollectionType(propType).Should().BeTrue();
    }

    /// <summary>
    /// Kills TypeAnalyzer line 104: string mutation on "System.Collections.ICollection".
    /// </summary>
    [Fact]
    public void IsCollectionType_DetectsICollection()
    {
        var source = @"
using System.Collections;
namespace TestApp;
public class Holder { public ICollection Items { get; set; } = new System.Collections.ArrayList(); }
";
        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.Holder");
        var propType = TestHarness.GetPropertySymbol(typeSymbol, "Items").Type;

        TypeAnalyzer.IsCollectionType(propType).Should().BeTrue();
    }

    /// <summary>
    /// Kills TypeAnalyzer line 101: string mutation on "System.Collections.Immutable.".
    /// </summary>
    [Fact]
    public void IsCollectionType_DetectsImmutableList()
    {
        var source = @"
using System.Collections.Immutable;
namespace TestApp;
public class Holder { public ImmutableList<string> Items { get; set; } = ImmutableList<string>.Empty; }
";
        var compilation = TestHarness.CreateCompilation(source);
        var typeSymbol = TestHarness.GetTypeSymbol(compilation, "TestApp.Holder");
        var propType = TestHarness.GetPropertySymbol(typeSymbol, "Items").Type;

        TypeAnalyzer.IsCollectionType(propType).Should().BeTrue();
    }

    /// <summary>
    /// Kills WithMethodEmitter line 48: negate IsCollectionType in GetTypeKey.
    /// If collection unwrap is negated, List&lt;Address&gt; and Address would NOT be seen as same type.
    /// This test has both a List&lt;Address&gt; and Address property — they should be treated as duplicates.
    /// </summary>
    [Fact]
    public void WithMethodEmitter_CollectionAndScalar_SameElementType_AreSeenAsDuplicates()
    {
        var source = @"
using Fabricate;
using System.Collections.Generic;
namespace TestApp;
public class Address { public string Street { get; set; } = """"; }
public class Person
{
    public string Name { get; set; } = """";
    public Address Home { get; set; } = new();
    public List<Address> PreviousAddresses { get; set; } = new();
}
[Fabricate<Person>]
public partial class PersonBuilder
{
    private static partial Person ValidInstance() => new() { Name = ""J"", Home = new(), PreviousAddresses = new() };
}
";
        var compilation = TestHarness.CreateCompilation(source);
        var result = TestHarness.RunGenerators(compilation, new BuilderGenerator());
        var generated = TestHarness.GetGeneratedSources(result)["PersonBuilder.g.cs"];

        // List<Address> element type = Address, plus scalar Address → duplicate → use property names
        generated.Should().Contain("WithHome(");
        generated.Should().Contain("WithPreviousAddresses(");
        // Neither should use just "With(" since Address appears in both
        generated.Should().NotContain("public PersonBuilder With(global::TestApp.Address");
    }

    /// <summary>
    /// Kills WithMethodEmitter lines 57-59: nullable struct unwrapping in GetTypeKey.
    /// Two properties: one Coordinate? and one Coordinate — after unwrap they're the same type key.
    /// </summary>
    [Fact]
    public void WithMethodEmitter_NullableStruct_AndScalar_AreSeenAsDuplicates()
    {
        var source = @"
using Fabricate;
namespace TestApp;
public struct Coordinate { public double X { get; set; } public double Y { get; set; } }
public class Map
{
    public string Name { get; set; } = """";
    public Coordinate Center { get; set; }
    public Coordinate? Marker { get; set; }
}
[Fabricate<Map>]
public partial class MapBuilder
{
    private static partial Map ValidInstance() => new() { Name = ""x"", Center = new(), Marker = new Coordinate() };
}
";
        var compilation = TestHarness.CreateCompilation(source);
        var result = TestHarness.RunGenerators(compilation, new BuilderGenerator());
        var generated = TestHarness.GetGeneratedSources(result)["MapBuilder.g.cs"];

        // Coordinate and Coordinate? → after nullable unwrap, both are Coordinate → duplicate
        generated.Should().Contain("WithCenter(");
        generated.Should().Contain("WithMarker(");
    }

    /// <summary>
    /// Kills WithMethodEmitter line 39: counts[typeKey]++ → counts[typeKey]--
    /// If decrement is used instead, duplicate types would show count 0 or negative
    /// and isTypeUnique would be wrong. We verify via generated method names.
    /// </summary>
    [Fact]
    public void WithMethodEmitter_CountsTypeOccurrences_AffectsMethodNaming()
    {
        var source = @"
using Fabricate;
namespace TestApp;
public class Address { public string Street { get; set; } = """"; }
public class Person
{
    public string Name { get; set; } = """";
    public Address Home { get; set; } = new();
    public Address Work { get; set; } = new();
}
[Fabricate<Person>]
public partial class PersonBuilder
{
    private static partial Person ValidInstance() => new() { Name = ""J"", Home = new(), Work = new() };
}
";
        var compilation = TestHarness.CreateCompilation(source);
        var result = TestHarness.RunGenerators(compilation, new BuilderGenerator());
        var generated = TestHarness.GetGeneratedSources(result)["PersonBuilder.g.cs"];

        // Duplicate Address type → must use property names
        generated.Should().Contain("WithHome(");
        generated.Should().Contain("WithWork(");
        // Should NOT have just "With(" for Address since it appears twice
        generated.Should().NotContain("public PersonBuilder With(global::TestApp.Address");
    }

    /// <summary>
    /// Kills WithMethodEmitter line 48: negate IsCollectionType in GetTypeKey.
    /// Collections should use element type as the key for uniqueness, not the full collection type.
    /// Two List&lt;Address&gt; properties should still count Address as duplicate.
    /// </summary>
    [Fact]
    public void WithMethodEmitter_CollectionElementType_UsedForUniquenessCheck()
    {
        var source = @"
using Fabricate;
using System.Collections.Generic;
namespace TestApp;
public class Tag { public string Value { get; set; } = """"; }
public class Article
{
    public string Title { get; set; } = """";
    public List<Tag> MainTags { get; set; } = new();
    public List<Tag> SecondaryTags { get; set; } = new();
}
[Fabricate<Article>]
public partial class ArticleBuilder
{
    private static partial Article ValidInstance() => new() { Title = ""x"", MainTags = new(), SecondaryTags = new() };
}
";
        var compilation = TestHarness.CreateCompilation(source);
        var result = TestHarness.RunGenerators(compilation, new BuilderGenerator());
        var generated = TestHarness.GetGeneratedSources(result)["ArticleBuilder.g.cs"];

        // Two List<Tag> properties → Tag is duplicate → must use property names
        generated.Should().Contain("WithMainTags(");
        generated.Should().Contain("WithSecondaryTags(");
    }

    /// <summary>
    /// Kills WithMethodEmitter lines 57-59: nullable unwrapping in GetTypeKey.
    /// Two properties of type Address? should count as duplicate Address (after unwrapping nullable).
    /// </summary>
    [Fact]
    public void WithMethodEmitter_NullableUnwrapping_InGetTypeKey_AffectsUniqueness()
    {
        var source = @"
using Fabricate;
namespace TestApp;
public class Address { public string Street { get; set; } = """"; }
public class Person
{
    public string Name { get; set; } = """";
    public Address? Home { get; set; }
    public Address? Work { get; set; }
}
[Fabricate<Person>]
public partial class PersonBuilder
{
    private static partial Person ValidInstance() => new() { Name = ""J"", Home = new(), Work = new() };
}
";
        var compilation = TestHarness.CreateCompilation(source);
        var result = TestHarness.RunGenerators(compilation, new BuilderGenerator());
        var generated = TestHarness.GetGeneratedSources(result)["PersonBuilder.g.cs"];

        // Two Address? properties → after nullable unwrap, both are Address → duplicate → use property names
        generated.Should().Contain("WithHome(");
        generated.Should().Contain("WithWork(");
    }

    /// <summary>
    /// Also verify that a single nullable custom type uses "With" (unique after unwrap).
    /// </summary>
    [Fact]
    public void WithMethodEmitter_SingleNullableCustomType_UsesWithOnly()
    {
        var source = @"
using Fabricate;
namespace TestApp;
public class Address { public string Street { get; set; } = """"; }
public class Person
{
    public string Name { get; set; } = """";
    public Address? Home { get; set; }
}
[Fabricate<Person>]
public partial class PersonBuilder
{
    private static partial Person ValidInstance() => new() { Name = ""J"", Home = new() };
}
";
        var compilation = TestHarness.CreateCompilation(source);
        var result = TestHarness.RunGenerators(compilation, new BuilderGenerator());
        var generated = TestHarness.GetGeneratedSources(result)["PersonBuilder.g.cs"];

        // Single Address? → unique after unwrap → just "With"
        generated.Should().Contain("public PersonBuilder With(");
    }

    /// <summary>
    /// Kills BuildMethodEmitter line 38: equality mutation on constructorParams.Length > 0.
    /// Verify that a class with NO constructor params and settable properties uses empty parens.
    /// </summary>
    [Fact]
    public void BuildMethodEmitter_NoConstructorParams_EmitsEmptyParentheses()
    {
        var source = @"
using Fabricate;
namespace TestApp;
public class Thing
{
    public string Name { get; set; } = """";
    public int Value { get; set; }
}
[Fabricate<Thing>]
public partial class ThingBuilder
{
    private static partial Thing ValidInstance() => new() { Name = ""x"", Value = 1 };
}
";
        var compilation = TestHarness.CreateCompilation(source);
        var result = TestHarness.RunGenerators(compilation, new BuilderGenerator());
        var generated = TestHarness.GetGeneratedSources(result)["ThingBuilder.g.cs"];

        // No constructor params + settable properties → "=> new Type()" with object initializer
        generated.Should().Contain("=> new global::TestApp.Thing()");
        generated.Should().Contain("Name = _name,");
        generated.Should().Contain("Value = _value,");
    }

    /// <summary>
    /// Verify that WITH constructor params, the params are emitted inside parens.
    /// This complements the above test to kill the equality mutation.
    /// </summary>
    [Fact]
    public void BuildMethodEmitter_WithConstructorParams_EmitsParamsInsideParens()
    {
        var source = @"
using Fabricate;
namespace TestApp;
public class Thing
{
    public string Name { get; }
    public Thing(string name) { Name = name; }
}
[Fabricate<Thing>]
public partial class ThingBuilder
{
    private static partial Thing ValidInstance() => new(""x"");
}
";
        var compilation = TestHarness.CreateCompilation(source);
        var result = TestHarness.RunGenerators(compilation, new BuilderGenerator());
        var generated = TestHarness.GetGeneratedSources(result)["ThingBuilder.g.cs"];

        // Constructor params → emitted inside parens
        generated.Should().Contain("=> new global::TestApp.Thing(_name);");
    }

    /// <summary>
    /// Kills FactoryGenerator line 41: statement mutation on early return for empty entries.
    /// When there are no builders, the factory should NOT be generated.
    /// </summary>
    [Fact]
    public void FactoryGenerator_EmptyEntries_ProducesNoOutput()
    {
        var source = @"
using Fabricate;
[assembly: FabricateFactory(""Create"")]
namespace TestApp;
public class Patient { public string Name { get; set; } = """"; }
";
        var compilation = TestHarness.CreateCompilation(source);
        var result = TestHarness.RunGenerators(compilation, new FactoryGenerator());

        result.GeneratedTrees.Should().BeEmpty();
    }
}
