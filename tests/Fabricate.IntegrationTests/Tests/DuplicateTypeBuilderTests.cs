using Fabricate.IntegrationTests.Builders;
using Fabricate.IntegrationTests.Models;
using FluentAssertions;
using Xunit;

namespace Fabricate.IntegrationTests.Tests;

public class DuplicateTypeBuilderTests
{
    [Fact]
    public void DuplicateCustomType_UsesPropertyNameInMethod()
    {
        var homeAddress = new Address { Street = "New Home", City = "A", ZipCode = "111" };
        var workAddress = new Address { Street = "New Work", City = "B", ZipCode = "222" };

        var person = new PersonWithTwoAddressesBuilder()
            .WithHomeAddress(homeAddress)
            .WithWorkAddress(workAddress)
            .Build();

        person.HomeAddress.Should().Be(homeAddress);
        person.WorkAddress.Should().Be(workAddress);
    }
}
