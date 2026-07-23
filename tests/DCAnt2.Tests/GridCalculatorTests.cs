using DCAnt2.Core.Domain;
using System;
using Xunit;

namespace DCAnt2.Tests;

public class GridCalculatorTests
{
    private readonly InstrumentRules _rules = new("USDT", 0.1m, 0.001m, 10m);

    [Fact]
    public void Calculate_GeneratesCorrectGrid()
    {
        var settings = new GridSettings(
            firstOrderVolume: new Money(100m),
            maxCapital: new Money(1000m),
            maxLevels: 2,
            baseStepPercent: 1.0m,
            stepScale: 2.0m,
            volumeScale: 2.0m
        );

        var plan = GridCalculator.Calculate(settings, new Price(10000m), _rules);

        Assert.Equal(3, plan.Levels.Count);
        
        // L0: 100 USDT at 10000 -> 0.01 Qty, 100 actual USDT
        Assert.Equal(0, plan.Levels[0].Index);
        Assert.Equal(10000m, plan.Levels[0].Price.Value);
        Assert.Equal(0.01m, plan.Levels[0].Quantity.Value);

        // L1: 200 USDT at 9900 (1% drop) -> 0.02 Qty, 198 actual USDT
        Assert.Equal(1, plan.Levels[1].Index);
        Assert.Equal(9900m, plan.Levels[1].Price.Value);
        Assert.Equal(0.02m, plan.Levels[1].Quantity.Value);

        // L2: 400 USDT at 9700 (1% + 2% drop) -> 0.041 Qty, 397.7 actual USDT
        Assert.Equal(2, plan.Levels[2].Index);
        Assert.Equal(9700m, plan.Levels[2].Price.Value);
        Assert.Equal(0.041m, plan.Levels[2].Quantity.Value);
    }

    [Fact]
    public void Calculate_ThrowsWhenLevelsOverlap()
    {
        var settings = new GridSettings(new Money(100m), new Money(1000m), 1, 0.0001m, 1.0m, 1.0m);
        var rules = new InstrumentRules("USDT", 10m, 0.001m, 10m);

        var ex = Assert.Throws<InvalidOperationException>(() => GridCalculator.Calculate(settings, new Price(10000m), rules));
        Assert.Contains("overlapped", ex.Message);
    }

    [Fact]
    public void Calculate_ThrowsWhenBelowMinNotional()
    {
        var settings = new GridSettings(new Money(5m), new Money(1000m), 1, 1.0m, 1.0m, 1.0m);
        
        var ex = Assert.Throws<InvalidOperationException>(() => GridCalculator.Calculate(settings, new Price(10000m), _rules));
        Assert.Contains("MinNotional", ex.Message);
    }

    [Fact]
    public void Calculate_ThrowsWhenMaxCapitalExceeded()
    {
        var settings = new GridSettings(new Money(100m), new Money(150m), 1, 1.0m, 1.0m, 1.0m);
        
        var ex = Assert.Throws<InvalidOperationException>(() => GridCalculator.Calculate(settings, new Price(10000m), _rules));
        Assert.Contains("MaxCapital", ex.Message);
    }
}
