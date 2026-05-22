using Fabricate;
using Fabricate.IntegrationTests.Models;

namespace Fabricate.IntegrationTests.Builders;

[Fabricate<Money>]
public partial class MoneyBuilder
{
    private static partial Money ValidInstance() => new(100.00m, "EUR");
}
