namespace Fabricate.IntegrationTests.Models;

public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string ZipCode { get; set; } = "";
}

public class PersonWithAddress
{
    public string Name { get; set; } = "";
    public Address HomeAddress { get; set; } = new();
}
