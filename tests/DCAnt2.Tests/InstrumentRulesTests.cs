using DCAnt2.Core.Domain;
using System;
using Xunit;

namespace DCAnt2.Tests;

public class InstrumentRulesTests
{
    private readonly InstrumentRules _rules;

    public InstrumentRulesTests()
    {
        _rules = new InstrumentRules("USDT", 0.5m, 0.001m, 10m);
    }

    [Theory]
    [InlineData(65000.1, 65000.0)]
    [InlineData(65000.25, 65000.5)]
    [InlineData(65000.3, 65000.5)]
    [InlineData(65000.75, 65001.0)]
    public void RoundPrice_RoundsToNearestTickSize(decimal input, decimal expected)
    {
        var price = new Price(input);
        var rounded = _rules.RoundPrice(price);
        Assert.Equal(expected, rounded.Value);
    }

    [Theory]
    [InlineData(1.0019, 1.001)]
    [InlineData(1.0011, 1.001)]
    [InlineData(0.001, 0.001)]
    [InlineData(0.001999, 0.001)]
    public void RoundQuantityDown_FloorsToQuantityStep(decimal input, decimal expected)
    {
        var qty = new Quantity(input);
        var rounded = _rules.RoundQuantityDown(qty);
        Assert.Equal(expected, rounded.Value);
    }
}
