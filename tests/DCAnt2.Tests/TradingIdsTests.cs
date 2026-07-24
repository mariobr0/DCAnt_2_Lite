using DCAnt2.Core.Domain;
using System;
using Xunit;

namespace DCAnt2.Tests;

public class TradingIdsTests
{
    // StrategyInstanceId
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StrategyInstanceId_InvalidValue_ThrowsArgumentException(string? value)
    {
        Assert.Throws<ArgumentException>(() => new StrategyInstanceId(value!));
    }

    [Fact]
    public void StrategyInstanceId_ValidValue_SavesValue()
    {
        var id = new StrategyInstanceId("strat-1");
        Assert.Equal("strat-1", id.Value);
        Assert.Equal("strat-1", id.ToString());
    }

    [Fact]
    public void StrategyInstanceId_SameValues_AreEqual()
    {
        var first = new StrategyInstanceId("strat-1");
        var second = new StrategyInstanceId("strat-1");
        
        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void StrategyInstanceId_DifferentValues_AreNotEqual()
    {
        var first = new StrategyInstanceId("strat-1");
        var second = new StrategyInstanceId("strat-2");
        
        Assert.NotEqual(first, second);
        Assert.True(first != second);
    }

    [Fact]
    public void StrategyInstanceId_New_CreatesInitializedUniqueIdsWithPrefix()
    {
        var first = StrategyInstanceId.New();
        var second = StrategyInstanceId.New();
        
        Assert.StartsWith("strat_", first.Value);
        Assert.StartsWith("strat_", second.Value);
        Assert.NotEqual(first, second);
    }

    // TradeCycleId
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TradeCycleId_InvalidValue_ThrowsArgumentException(string? value)
    {
        Assert.Throws<ArgumentException>(() => new TradeCycleId(value!));
    }

    [Fact]
    public void TradeCycleId_ValidValue_SavesValue()
    {
        var id = new TradeCycleId("cyc-1");
        Assert.Equal("cyc-1", id.Value);
        Assert.Equal("cyc-1", id.ToString());
    }

    [Fact]
    public void TradeCycleId_SameValues_AreEqual()
    {
        var first = new TradeCycleId("cyc-1");
        var second = new TradeCycleId("cyc-1");
        
        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void TradeCycleId_DifferentValues_AreNotEqual()
    {
        var first = new TradeCycleId("cyc-1");
        var second = new TradeCycleId("cyc-2");
        
        Assert.NotEqual(first, second);
        Assert.True(first != second);
    }

    [Fact]
    public void TradeCycleId_New_CreatesInitializedUniqueIdsWithPrefix()
    {
        var first = TradeCycleId.New();
        var second = TradeCycleId.New();
        
        Assert.StartsWith("cyc_", first.Value);
        Assert.StartsWith("cyc_", second.Value);
        Assert.NotEqual(first, second);
    }

    // InternalOrderId
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void InternalOrderId_InvalidValue_ThrowsArgumentException(string? value)
    {
        Assert.Throws<ArgumentException>(() => new InternalOrderId(value!));
    }

    [Fact]
    public void InternalOrderId_ValidValue_SavesValue()
    {
        var id = new InternalOrderId("ord-1");
        Assert.Equal("ord-1", id.Value);
        Assert.Equal("ord-1", id.ToString());
    }

    [Fact]
    public void InternalOrderId_SameValues_AreEqual()
    {
        var first = new InternalOrderId("ord-1");
        var second = new InternalOrderId("ord-1");
        
        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void InternalOrderId_DifferentValues_AreNotEqual()
    {
        var first = new InternalOrderId("ord-1");
        var second = new InternalOrderId("ord-2");
        
        Assert.NotEqual(first, second);
        Assert.True(first != second);
    }

    [Fact]
    public void InternalOrderId_New_CreatesInitializedUniqueIdsWithPrefix()
    {
        var first = InternalOrderId.New();
        var second = InternalOrderId.New();
        
        Assert.StartsWith("ord_", first.Value);
        Assert.StartsWith("ord_", second.Value);
        Assert.NotEqual(first, second);
    }

    // EffectId
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EffectId_InvalidValue_ThrowsArgumentException(string? value)
    {
        Assert.Throws<ArgumentException>(() => new EffectId(value!));
    }

    [Fact]
    public void EffectId_ValidValue_SavesValue()
    {
        var id = new EffectId("eff-1");
        Assert.Equal("eff-1", id.Value);
        Assert.Equal("eff-1", id.ToString());
    }

    [Fact]
    public void EffectId_SameValues_AreEqual()
    {
        var first = new EffectId("eff-1");
        var second = new EffectId("eff-1");
        
        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void EffectId_DifferentValues_AreNotEqual()
    {
        var first = new EffectId("eff-1");
        var second = new EffectId("eff-2");
        
        Assert.NotEqual(first, second);
        Assert.True(first != second);
    }

    [Fact]
    public void EffectId_New_CreatesInitializedUniqueIdsWithPrefix()
    {
        var first = EffectId.New();
        var second = EffectId.New();
        
        Assert.StartsWith("eff_", first.Value);
        Assert.StartsWith("eff_", second.Value);
        Assert.NotEqual(first, second);
    }
}
