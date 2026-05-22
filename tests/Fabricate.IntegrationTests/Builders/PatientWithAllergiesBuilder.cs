using Fabricate;
using Fabricate.IntegrationTests.Models;

namespace Fabricate.IntegrationTests.Builders;

[Fabricate<PatientWithAllergies>]
public partial class PatientWithAllergiesBuilder
{
    private static partial PatientWithAllergies ValidInstance() => new()
    {
        Name = "Allergy Patient",
        Allergies = new List<string> { "Peanuts", "Shellfish" }
    };
}
