using Fabricate;
using Fabricate.IntegrationTests.Models;

namespace Fabricate.IntegrationTests.Builders;

[Fabricate<Patient>]
public partial class PatientBuilder
{
    private static partial Patient ValidInstance() => new()
    {
        Name = "John Doe",
        Age = 30,
        MiddleName = null,
        DateOfBirth = new DateTime(1994, 1, 15)
    };
}
