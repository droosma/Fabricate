using Fabricate.IntegrationTests.Builders;
using FluentAssertions;
using Xunit;

namespace Fabricate.IntegrationTests.Tests;

public class CollectionBuilderTests
{
    [Fact]
    public void Build_WithCollectionDefaults()
    {
        var patient = new PatientWithAllergiesBuilder().Build();

        patient.Allergies.Should().Contain("Peanuts");
        patient.Allergies.Should().Contain("Shellfish");
    }

    [Fact]
    public void WithAllergies_Params_OverridesCollection()
    {
        var patient = new PatientWithAllergiesBuilder()
            .WithAllergies("Dust", "Mold")
            .Build();

        patient.Allergies.Should().BeEquivalentTo(new[] { "Dust", "Mold" });
    }
}
