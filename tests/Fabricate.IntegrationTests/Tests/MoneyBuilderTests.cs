using Fabricate.IntegrationTests.Builders;
using FluentAssertions;
using Xunit;

namespace Fabricate.IntegrationTests.Tests;

public class MoneyBuilderTests
{
    [Fact]
    public void Build_ReadonlyRecordStruct()
    {
        var money = new MoneyBuilder().Build();

        money.Amount.Should().Be(100.00m);
        money.Currency.Should().Be("EUR");
    }

    [Fact]
    public void WithAmount_Overrides()
    {
        var money = new MoneyBuilder().WithAmount(50.00m).Build();

        money.Amount.Should().Be(50.00m);
        money.Currency.Should().Be("EUR");
    }
}
