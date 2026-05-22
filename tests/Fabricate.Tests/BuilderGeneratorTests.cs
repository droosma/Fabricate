using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using FluentAssertions;

namespace Fabricate.Tests;

public class BuilderGeneratorTests
{
    [Fact]
    public async Task Generator_ProducesOutput_ForSimpleClass()
    {
        var source = @"
using Fabricate;

namespace TestApp;

public class Patient
{
    public string Name { get; set; } = """";
    public int Age { get; set; }
}

[Fabricate<Patient>]
public partial class PatientBuilder
{
    private static partial Patient ValidInstance() => new()
    {
        Name = ""John"",
        Age = 30
    };
}
";

        var generatedSources = await RunGenerator(source);
        generatedSources.Should().ContainKey("PatientBuilder.g.cs");

        var generated = generatedSources["PatientBuilder.g.cs"];
        generated.Should().Contain("partial class PatientBuilder");
        generated.Should().Contain("public PatientBuilder WithName(");
        generated.Should().Contain("public PatientBuilder WithAge(");
        generated.Should().Contain("Build()");
        generated.Should().Contain("implicit operator");
    }

    [Fact]
    public async Task Generator_ProducesOutput_ForRecord()
    {
        var source = @"
using Fabricate;

namespace TestApp;

public record Appointment(string DoctorName, System.DateTime ScheduledAt);

[Fabricate<Appointment>]
public partial class AppointmentBuilder
{
    private static partial Appointment ValidInstance() => new(""Dr. Smith"", new System.DateTime(2026, 1, 1));
}
";

        var generatedSources = await RunGenerator(source);
        generatedSources.Should().ContainKey("AppointmentBuilder.g.cs");

        var generated = generatedSources["AppointmentBuilder.g.cs"];
        generated.Should().Contain("public AppointmentBuilder WithDoctorName(");
        generated.Should().Contain("public AppointmentBuilder WithScheduledAt(");
        generated.Should().Contain("=> new global::TestApp.Appointment(_doctorName, _scheduledAt);");
    }

    [Fact]
    public async Task Generator_ProducesOutput_ForReadonlyRecordStruct()
    {
        var source = @"
using Fabricate;

namespace TestApp;

public readonly record struct Money(decimal Amount, string Currency);

[Fabricate<Money>]
public partial class MoneyBuilder
{
    private static partial Money ValidInstance() => new(100m, ""EUR"");
}
";

        var generatedSources = await RunGenerator(source);
        generatedSources.Should().ContainKey("MoneyBuilder.g.cs");

        var generated = generatedSources["MoneyBuilder.g.cs"];
        generated.Should().Contain("public MoneyBuilder WithAmount(");
        generated.Should().Contain("public MoneyBuilder WithCurrency(");
        generated.Should().Contain("=> new global::TestApp.Money(_amount, _currency);");
    }

    [Fact]
    public async Task Generator_HandlesNullableProperties()
    {
        var source = @"
#nullable enable
using Fabricate;

namespace TestApp;

public class Person
{
    public string Name { get; set; } = """";
    public string? MiddleName { get; set; }
}

[Fabricate<Person>]
public partial class PersonBuilder
{
    private static partial Person ValidInstance() => new()
    {
        Name = ""John"",
        MiddleName = null
    };
}
";

        var generatedSources = await RunGenerator(source);
        var generated = generatedSources["PersonBuilder.g.cs"];
        generated.Should().Contain("WithMiddleName(");
        generated.Should().Contain("WithoutMiddleName()");
    }

    [Fact]
    public async Task Generator_HandlesCollections()
    {
        var source = @"
using Fabricate;
using System.Collections.Generic;

namespace TestApp;

public class PatientWithAllergies
{
    public string Name { get; set; } = """";
    public List<string> Allergies { get; set; } = new();
}

[Fabricate<PatientWithAllergies>]
public partial class PatientWithAllergiesBuilder
{
    private static partial PatientWithAllergies ValidInstance() => new()
    {
        Name = ""Test"",
        Allergies = new List<string> { ""Peanuts"" }
    };
}
";

        var generatedSources = await RunGenerator(source);
        var generated = generatedSources["PatientWithAllergiesBuilder.g.cs"];
        generated.Should().Contain("WithAllergies(params");
        generated.Should().Contain("Allergies = new global::System.Collections.Generic.List<string>(_allergies),");
    }

    [Fact]
    public async Task Generator_UniqueCustomType_UsesWithOverload()
    {
        var source = @"
using Fabricate;

namespace TestApp;

public class Address
{
    public string Street { get; set; } = """";
}

public class PersonWithAddress
{
    public string Name { get; set; } = """";
    public Address HomeAddress { get; set; } = new();
}

[Fabricate<PersonWithAddress>]
public partial class PersonWithAddressBuilder
{
    private static partial PersonWithAddress ValidInstance() => new()
    {
        Name = ""John"",
        HomeAddress = new Address { Street = ""123 Main"" }
    };
}
";

        var generatedSources = await RunGenerator(source);
        var generated = generatedSources["PersonWithAddressBuilder.g.cs"];
        // Unique custom type should just be "With" not "WithHomeAddress"
        generated.Should().Contain("public PersonWithAddressBuilder With(");
    }

    [Fact]
    public async Task Generator_DuplicateCustomType_UsesPropertyNameInMethod()
    {
        var source = @"
using Fabricate;

namespace TestApp;

public class Address
{
    public string Street { get; set; } = """";
}

public class PersonWithTwoAddresses
{
    public string Name { get; set; } = """";
    public Address HomeAddress { get; set; } = new();
    public Address WorkAddress { get; set; } = new();
}

[Fabricate<PersonWithTwoAddresses>]
public partial class PersonWithTwoAddressesBuilder
{
    private static partial PersonWithTwoAddresses ValidInstance() => new()
    {
        Name = ""John"",
        HomeAddress = new Address { Street = ""Home"" },
        WorkAddress = new Address { Street = ""Work"" }
    };
}
";

        var generatedSources = await RunGenerator(source);
        var generated = generatedSources["PersonWithTwoAddressesBuilder.g.cs"];
        // Duplicate custom type should include property name
        generated.Should().Contain("WithHomeAddress(");
        generated.Should().Contain("WithWorkAddress(");
    }

    [Fact]
    public async Task Generator_InheritedProperties_AreFlattened()
    {
        var source = @"
using Fabricate;

namespace TestApp;

public class Person
{
    public string Name { get; set; } = """";
}

public class Patient : Person
{
    public int Age { get; set; }
}

[Fabricate<Patient>]
public partial class PatientBuilder
{
    private static partial Patient ValidInstance() => new()
    {
        Name = ""John"",
        Age = 30
    };
}
";

        var generatedSources = await RunGenerator(source);
        var generated = generatedSources["PatientBuilder.g.cs"];
        generated.Should().Contain("WithName(");
        generated.Should().Contain("WithAge(");
    }

    private static async Task<Dictionary<string, string>> RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        // Add reference to the Fabricate assembly for the attribute
        references.Add(MetadataReference.CreateFromFile(typeof(FabricateAttribute<>).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var generator = new BuilderGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var result = driver.GetRunResult();
        
        return result.GeneratedTrees
            .ToDictionary(
                t => Path.GetFileName(t.FilePath),
                t => t.GetText().ToString());
    }
}
