using Fabricate;
using Fabricate.IntegrationTests.Models;

namespace Fabricate.IntegrationTests.Builders;

[Fabricate<Appointment>]
public partial class AppointmentBuilder
{
    private static partial Appointment ValidInstance() => new("Dr. Smith", new DateTime(2026, 6, 15, 10, 0, 0))
    {
        Notes = "Regular checkup"
    };
}
