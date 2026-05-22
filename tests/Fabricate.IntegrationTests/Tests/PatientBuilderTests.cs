using Fabricate.IntegrationTests.Builders;
using Fabricate.IntegrationTests.Models;
using FluentAssertions;
using Xunit;

namespace Fabricate.IntegrationTests.Tests;

public class PatientBuilderTests
{
    [Fact]
    public void Build_ReturnsValidInstance_WithDefaults()
    {
        var patient = new PatientBuilder().Build();

        patient.Name.Should().Be("John Doe");
        patient.Age.Should().Be(30);
        patient.MiddleName.Should().BeNull();
        patient.DateOfBirth.Should().Be(new DateTime(1994, 1, 15));
    }

    [Fact]
    public void WithName_OverridesDefault()
    {
        var patient = new PatientBuilder().WithName("Jane").Build();

        patient.Name.Should().Be("Jane");
        patient.Age.Should().Be(30);
    }

    [Fact]
    public void WithAge_OverridesDefault()
    {
        var patient = new PatientBuilder().WithAge(25).Build();

        patient.Age.Should().Be(25);
        patient.Name.Should().Be("John Doe");
    }

    [Fact]
    public void WithMiddleName_SetsNullableProperty()
    {
        var patient = new PatientBuilder().WithMiddleName("Marie").Build();

        patient.MiddleName.Should().Be("Marie");
    }

    [Fact]
    public void WithoutMiddleName_SetsToNull()
    {
        var patient = new PatientBuilder()
            .WithMiddleName("Marie")
            .WithoutMiddleName()
            .Build();

        patient.MiddleName.Should().BeNull();
    }

    [Fact]
    public void ImplicitOperator_ConvertsToPatient()
    {
        Patient patient = new PatientBuilder().WithName("Implicit");

        patient.Name.Should().Be("Implicit");
    }
}
