using DCAnt2.Core.Domain;
using System;
using Xunit;

namespace DCAnt2.Tests;

public class GridSettingsTests
{
    [Fact]
    public void Constructor_WhenFirstOrderVolumeExceedsMaxCapital_ThrowsArgumentOutOfRangeException()
    {
        var firstOrderVol = new Money(150m);
        var maxCap = new Money(100m);
        
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => 
            new GridSettings(firstOrderVol, maxCap, 5, new Percentage(1m), 1m, 1m));
            
        Assert.Equal("firstOrderVolume", ex.ParamName);
    }

    [Fact]
    public void Constructor_WhenFirstOrderVolumeEqualsMaxCapital_IsAllowed()
    {
        var firstOrderVol = new Money(100m);
        var maxCap = new Money(100m);
        
        var settings = new GridSettings(firstOrderVol, maxCap, 5, new Percentage(1m), 1m, 1m);
            
        Assert.Equal(100m, settings.FirstOrderVolume.Value);
        Assert.Equal(100m, settings.MaxCapital.Value);
    }

    [Fact]
    public void Constructor_WhenMaxLevelsExceeds1000_ThrowsArgumentOutOfRangeException()
    {
        var firstOrderVol = new Money(100m);
        var maxCap = new Money(1000m);
        
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => 
            new GridSettings(firstOrderVol, maxCap, 1001, new Percentage(1m), 1m, 1m));
            
        Assert.Equal("maxLevels", ex.ParamName);
    }
}
