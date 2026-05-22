namespace Fabricate.IntegrationTests.Models;

public class PersonWithTwoAddresses
{
    public string Name { get; set; } = "";
    public Address HomeAddress { get; set; } = new();
    public Address WorkAddress { get; set; } = new();
}
