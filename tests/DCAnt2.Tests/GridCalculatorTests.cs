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
            baseStepPercent: new Percentage(1.0m),
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
        var settings = new GridSettings(new Money(100m), new Money(1000m), 1, new Percentage(0.0001m), 1.0m, 1.0m);
        var rules = new InstrumentRules("USDT", 10m, 0.001m, 10m);

        var ex = Assert.Throws<InvalidOperationException>(() => GridCalculator.Calculate(settings, new Price(10000m), rules));
        Assert.Contains("overlapped", ex.Message);
    }

    [Fact]
    public void Calculate_ThrowsWhenBelowMinNotional()
    {
        var settings = new GridSettings(new Money(1m), new Money(1000m), 1, new Percentage(1.0m), 1.0m, 1.0m);
        
        var ex = Assert.Throws<InvalidOperationException>(() => GridCalculator.Calculate(settings, new Price(1000m), _rules));
        Assert.Contains("MinNotional", ex.Message);
    }

    [Fact]
    public void Calculate_ThrowsWhenMaxCapitalExceeded()
    {
        var settings = new GridSettings(new Money(100m), new Money(150m), 1, new Percentage(1.0m), 1.0m, 1.0m);
        
        var ex = Assert.Throws<InvalidOperationException>(() => GridCalculator.Calculate(settings, new Price(10000m), _rules));
        Assert.Contains("MaxCapital", ex.Message);
    }

    [Fact]
    public void Calculate_WithNullSettings_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => GridCalculator.Calculate(null!, new Price(10000m), _rules));
        Assert.Equal("settings", ex.ParamName);
    }

    [Fact]
    public void Calculate_WithNullRules_ThrowsArgumentNullException()
    {
        var settings = new GridSettings(new Money(100m), new Money(1000m), 2, new Percentage(1.0m), 2.0m, 2.0m);
        var ex = Assert.Throws<ArgumentNullException>(() => GridCalculator.Calculate(settings, new Price(10000m), null!));
        Assert.Equal("rules", ex.ParamName);
    }

    [Fact]
    public void Calculate_WithZeroFirstOrderPrice_ThrowsArgumentOutOfRangeException()
    {
        var settings = new GridSettings(new Money(100m), new Money(1000m), 2, new Percentage(1.0m), 2.0m, 2.0m);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => GridCalculator.Calculate(settings, new Price(0m), _rules));
        Assert.Equal("firstOrderPrice", ex.ParamName);
    }

    [Fact]
    public void Calculate_CumulativeDistanceEquals100_ThrowsInvalidOperationException()
    {
        // 1st DCA level will drop 100% -> price = 0
        var settings = new GridSettings(new Money(100m), new Money(1000m), 1, new Percentage(100.0m), 1.0m, 1.0m);
        var ex = Assert.Throws<InvalidOperationException>(() => GridCalculator.Calculate(settings, new Price(10000m), _rules));
        Assert.Contains("non-positive", ex.Message);
    }

    [Fact]
    public void Calculate_CumulativeDistanceGreaterThan100_ThrowsInvalidOperationException()
    {
        // 1st DCA level will drop 105% -> price < 0
        var settings = new GridSettings(new Money(100m), new Money(1000m), 1, new Percentage(105.0m), 1.0m, 1.0m);
        var ex = Assert.Throws<InvalidOperationException>(() => GridCalculator.Calculate(settings, new Price(10000m), _rules));
        Assert.Contains("non-positive", ex.Message);
    }

    [Fact]
    public void Calculate_PriceRoundedToZero_ThrowsInvalidOperationException()
    {
        // Price = 0.04, tick = 0.1 -> rounds to 0
        var rules = new InstrumentRules("USDT", 0.1m, 1m, 1m);
        var settings = new GridSettings(new Money(100m), new Money(1000m), 0, new Percentage(1.0m), 1.0m, 1.0m);
        var ex = Assert.Throws<InvalidOperationException>(() => GridCalculator.Calculate(settings, new Price(0.04m), rules));
        Assert.Contains("rounded", ex.Message);
    }

    [Fact]
    public void Calculate_WithFractionalScales_ProducesExpectedPlan()
    {
        var settings = new GridSettings(
            firstOrderVolume: new Money(100m),
            maxCapital: new Money(1000m),
            maxLevels: 1,
            baseStepPercent: new Percentage(1.0m),
            stepScale: 1.5m, 
            volumeScale: 1.25m
        );

        var plan = GridCalculator.Calculate(settings, new Price(10000m), _rules);
        Assert.Equal(2, plan.Levels.Count);

        Assert.Equal(10000m, plan.Levels[0].Price.Value);
        Assert.Equal(9900m, plan.Levels[1].Price.Value);
        Assert.Equal(0.012m, plan.Levels[1].Quantity.Value);
    }

    [Fact]
    public void Calculate_WithMaxLevelsZero_ProducesOnlyFirstOrder()
    {
        var settings = new GridSettings(
            firstOrderVolume: new Money(100m),
            maxCapital: new Money(1000m),
            maxLevels: 0,
            baseStepPercent: new Percentage(1.0m),
            stepScale: 1.0m,
            volumeScale: 1.0m
        );

        var plan = GridCalculator.Calculate(settings, new Price(10000m), _rules);
        
        Assert.Single(plan.Levels);
        Assert.Equal(0, plan.Levels[0].Index);
        Assert.Equal(10000m, plan.Levels[0].Price.Value);
    }

    [Fact]
    public void Calculate_WhenVolumeScaleOverflows_ThrowsOverflowException()
    {
        var settings = new GridSettings(
            firstOrderVolume: new Money(100m),
            maxCapital: new Money(1000000000m), 
            maxLevels: 1, 
            baseStepPercent: new Percentage(1.0m),
            stepScale: 1.0m,
            volumeScale: decimal.MaxValue
        );

        Assert.Throws<OverflowException>(() => GridCalculator.Calculate(settings, new Price(10000m), _rules));
    }
}

