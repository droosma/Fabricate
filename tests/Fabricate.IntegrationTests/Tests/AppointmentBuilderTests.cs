using Fabricate.IntegrationTests.Builders;
using FluentAssertions;
using Xunit;

namespace Fabricate.IntegrationTests.Tests;

public class AppointmentBuilderTests
{
    [Fact]
    public void Build_Record_WithConstructorParams()
    {
        var appointment = new AppointmentBuilder().Build();

        appointment.DoctorName.Should().Be("Dr. Smith");
        appointment.ScheduledAt.Should().Be(new DateTime(2026, 6, 15, 10, 0, 0));
        appointment.Notes.Should().Be("Regular checkup");
    }

    [Fact]
    public void WithDoctorName_OverridesConstructorParam()
    {
        var appointment = new AppointmentBuilder().WithDoctorName("Dr. Jones").Build();

        appointment.DoctorName.Should().Be("Dr. Jones");
    }

    [Fact]
    public void WithoutNotes_SetsToNull()
    {
        var appointment = new AppointmentBuilder().WithoutNotes().Build();

        appointment.Notes.Should().BeNull();
    }
}
