using FluentAssertions;
using Xunit;

namespace Fabricate.Tests;

public class FabricateFactoryAttributeTests
{
    [Fact]
    public void Constructor_UsesProvidedName()
    {
        var attribute = new FabricateFactoryAttribute("Given");

        attribute.Name.Should().Be("Given");
    }

    [Fact]
    public void Constructor_UsesDefaultName()
    {
        var attribute = new FabricateFactoryAttribute();

        attribute.Name.Should().Be("A");
    }
}
