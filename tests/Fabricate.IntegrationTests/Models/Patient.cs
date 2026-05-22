namespace Fabricate.IntegrationTests.Models;

public class Patient
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string? MiddleName { get; set; }
    public DateTime DateOfBirth { get; set; }
}
