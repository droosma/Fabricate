namespace Fabricate.IntegrationTests.Models;

public record Appointment(string DoctorName, DateTime ScheduledAt)
{
    public string? Notes { get; init; }
}
