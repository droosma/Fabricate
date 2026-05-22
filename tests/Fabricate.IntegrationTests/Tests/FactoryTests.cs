using Fabricate.IntegrationTests.Builders;
using FluentAssertions;
using Xunit;

namespace Fabricate.IntegrationTests.Tests;

public class FactoryTests
{
    [Fact]
    public void Factory_A_CreatesBuilderInstance()
    {
        var patient = A.Patient.Build();

        patient.Name.Should().Be("John Doe");
        patient.Age.Should().Be(30);
    }

    [Fact]
    public void Factory_A_SupportsChaining()
    {
        var patient = A.Patient.WithName("Factory Patient").Build();

        patient.Name.Should().Be("Factory Patient");
    }
}
