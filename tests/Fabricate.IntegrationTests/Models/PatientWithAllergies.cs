namespace Fabricate.IntegrationTests.Models;

public class PatientWithAllergies
{
    public string Name { get; set; } = "";
    public List<string> Allergies { get; set; } = new();
}
