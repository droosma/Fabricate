using Fabricate;
using Fabricate.IntegrationTests.Models;

namespace Fabricate.IntegrationTests.Builders;

[Fabricate<PersonWithTwoAddresses>]
public partial class PersonWithTwoAddressesBuilder
{
    private static partial PersonWithTwoAddresses ValidInstance() => new()
    {
        Name = "Multi-Address Person",
        HomeAddress = new Address { Street = "1 Home St", City = "HomeTown", ZipCode = "11111" },
        WorkAddress = new Address { Street = "2 Work Ave", City = "WorkCity", ZipCode = "22222" }
    };
}
