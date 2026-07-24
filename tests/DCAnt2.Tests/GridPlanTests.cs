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
    public void Constructor_CreatesDefensiveCopy()
    {
        var source = new List<GridLevel>
        {
            new(0, new Price(100m), new Quantity(1m))
        };

        var plan = new GridPlan(source);

        source.Clear();

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
}
