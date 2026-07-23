using DCAnt2.Core.Domain;
using System;
using Xunit;

namespace DCAnt2.Tests;

public class FinancialTypesTests
{
    [Fact]
    public void CannotCreateNegativeMoney()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(-1m));
    }

    [Fact]
    public void CannotCreateNegativeQuantity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Quantity(-0.1m));
    }

    [Fact]
    public void CannotCreateNegativePrice()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Price(-100m));
    }
    
    [Fact]
    public void Money_AdditionAndSubtraction_Works()
    {
        var m1 = new Money(10m);
        var m2 = new Money(5m);
        
        var sum = m1 + m2;
        var diff = m1 - m2;
        
        Assert.Equal(15m, sum.Value);
        Assert.Equal(5m, diff.Value);
    }
}
