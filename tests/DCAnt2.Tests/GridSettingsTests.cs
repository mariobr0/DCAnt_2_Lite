using DCAnt2.Core.Domain;
using System;
using Xunit;

namespace DCAnt2.Tests;

public class GridSettingsTests
{
    [Fact]
    public void Constructor_WhenFirstOrderVolumeExceedsMaxCapital_ThrowsArgumentException()
    {
        var firstOrderVol = new Money(150m);
        var maxCap = new Money(100m);
        
        var ex = Assert.Throws<ArgumentException>(() => 
            new GridSettings(firstOrderVol, maxCap, 5, 1m, 1m, 1m));
            
        Assert.Contains("exceed MaxCapital", ex.Message);
    }

    [Fact]
    public void Constructor_WhenFirstOrderVolumeEqualsMaxCapital_IsAllowed()
    {
        var firstOrderVol = new Money(100m);
        var maxCap = new Money(100m);
        
        var settings = new GridSettings(firstOrderVol, maxCap, 5, 1m, 1m, 1m);
            
        Assert.Equal(100m, settings.FirstOrderVolume.Value);
        Assert.Equal(100m, settings.MaxCapital.Value);
    }
}
