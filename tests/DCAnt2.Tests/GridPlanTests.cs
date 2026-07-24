using System;
using System.Collections.Generic;
using DCAnt2.Core.Domain;
using Xunit;

namespace DCAnt2.Tests;

public class GridPlanTests
{
    [Fact]
    public void Constructor_WithNullLevels_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GridPlan(null!));
    }

    [Fact]
    public void Constructor_WithEmptyLevels_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new GridPlan(new List<GridLevel>()));
        Assert.Contains("empty", ex.Message);
    }

    [Fact]
    public void Constructor_WithNonSequentialIndexes_ThrowsArgumentException()
    {
        var levels = new List<GridLevel>
        {
            new(0, new Price(100m), new Quantity(1m)),
            new(2, new Price(90m), new Quantity(2m)) // Skipped index 1
        };

        var ex = Assert.Throws<ArgumentException>(() => new GridPlan(levels));
        Assert.Contains("must have index 1", ex.Message);
    }

    [Fact]
    public void Constructor_WithZeroPrice_ThrowsArgumentException()
    {
        var levels = new List<GridLevel>
        {
            new(0, new Price(0m), new Quantity(1m))
        };

        var ex = Assert.Throws<ArgumentException>(() => new GridPlan(levels));
        Assert.Contains("positive price", ex.Message);
    }

    [Fact]
    public void Constructor_WithZeroQuantity_ThrowsArgumentException()
    {
        var levels = new List<GridLevel>
        {
            new(0, new Price(100m), new Quantity(0m))
        };

        var ex = Assert.Throws<ArgumentException>(() => new GridPlan(levels));
        Assert.Contains("positive quantity", ex.Message);
    }

    [Fact]
    public void Constructor_DoesNotMutateWhenSourceListChanges()
    {
        var entry = new GridLevel(0, new Price(100m), new Quantity(1m));
        var source = new List<GridLevel> { entry };

        var plan = new GridPlan(source);

        source.Add(new GridLevel(1, new Price(90m), new Quantity(2m)));

        Assert.Single(plan.Levels);
    }

    [Fact]
    public void Levels_CannotBeCastToMutableArray()
    {
        var source = new List<GridLevel>
        {
            new(0, new Price(100m), new Quantity(1m))
        };

        var plan = new GridPlan(source);

        Assert.False(plan.Levels is GridLevel[]);
    }

    [Fact]
    public void Constructor_CalculatesAggregatesCorrectly_SingleEntry()
    {
        var levels = new List<GridLevel>
        {
            new(0, new Price(100m), new Quantity(1.5m))
        };

        var plan = new GridPlan(levels);

        Assert.Equal(1.5m, plan.TotalQuantity.Value);
        Assert.Equal(150m, plan.TotalNotional.Value);
        Assert.Equal(100m, plan.ExpectedVwap.Value);
    }

    [Fact]
    public void Constructor_CalculatesAggregatesCorrectly_MultiLevel_FractionalVwap()
    {
        var levels = new List<GridLevel>
        {
            new(0, new Price(100m), new Quantity(1m)), // notional = 100
            new(1, new Price(90m), new Quantity(2m)),  // notional = 180
            new(2, new Price(80m), new Quantity(4m))   // notional = 320
        };

        // Total Qty = 7
        // Total Notional = 600
        // Expected VWAP = 600 / 7 = 85.7142857142857... (fractional)

        var plan = new GridPlan(levels);

        Assert.Equal(7m, plan.TotalQuantity.Value);
        Assert.Equal(600m, plan.TotalNotional.Value);
        Assert.Equal(600m / 7m, plan.ExpectedVwap.Value);
    }
}
