using Fabricate;
using Fabricate.IntegrationTests.Models;

namespace Fabricate.IntegrationTests.Builders;

[Fabricate<PersonWithAddress>]
public partial class PersonWithAddressBuilder
{
    private static partial PersonWithAddress ValidInstance() => new()
    {
        Name = "Jane Smith",
        HomeAddress = new Address
        {
            Street = "123 Main St",
            City = "Springfield",
            ZipCode = "12345"
        }
    };
}
