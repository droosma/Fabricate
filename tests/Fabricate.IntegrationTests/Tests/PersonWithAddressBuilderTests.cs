using Fabricate.IntegrationTests.Builders;
using Fabricate.IntegrationTests.Models;
using FluentAssertions;
using Xunit;

namespace Fabricate.IntegrationTests.Tests;

public class PersonWithAddressBuilderTests
{
    [Fact]
    public void Build_ReturnsValidInstance_WithCustomType()
    {
        var person = new PersonWithAddressBuilder().Build();

        person.Name.Should().Be("Jane Smith");
        person.HomeAddress.Street.Should().Be("123 Main St");
        person.HomeAddress.City.Should().Be("Springfield");
    }

    [Fact]
    public void With_CustomType_UniqueOverload()
    {
        var newAddress = new Address { Street = "456 Oak Ave", City = "Portland", ZipCode = "97201" };
        var person = new PersonWithAddressBuilder().With(newAddress).Build();

        person.HomeAddress.Should().Be(newAddress);
    }
}
