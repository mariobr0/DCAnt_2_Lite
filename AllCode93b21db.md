# All Code in 93b21db
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\Domain\FinancialTypes.cs
```csharp
using System;

namespace DCAnt2.Core.Domain;

public readonly record struct Money
{
    public decimal Value { get; }

    public static Money Zero => new(0m);
    
    public Money(decimal value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Money cannot be negative.");
        Value = value;
    }
    
    public static Money operator +(Money a, Money b) => new(a.Value + b.Value);
    public static Money operator -(Money a, Money b) => new(a.Value - b.Value);
    
    public override string ToString() => Value.ToString("0.########");
}

public readonly record struct Quantity
{
    public decimal Value { get; }

    public static Quantity Zero => new(0m);
    
    public Quantity(decimal value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Quantity cannot be negative.");
        Value = value;
    }
    
    public static Quantity operator +(Quantity a, Quantity b) => new(a.Value + b.Value);
    public static Quantity operator -(Quantity a, Quantity b) => new(a.Value - b.Value);
    
    public override string ToString() => Value.ToString("0.########");
}

public readonly record struct Price
{
    public decimal Value { get; }

    public static Price Zero => new(0m);
    
    public Price(decimal value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Price cannot be negative.");
        Value = value;
    }
    
    public override string ToString() => Value.ToString("0.########");
}

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\Domain\GridCalculator.cs
```csharp
using System;
using System.Collections.Generic;

namespace DCAnt2.Core.Domain;

public static class GridCalculator
{
    public static GridPlan Calculate(GridSettings settings, Price firstOrderPrice, InstrumentRules rules)
    {
        var levels = new List<GridLevel>(settings.MaxLevels + 1);
        
        var currentPrice = firstOrderPrice;
        decimal currentDistancePercent = 0;
        
        var currentVolume = settings.FirstOrderVolume;
        Money totalCapitalUsed = Money.Zero;

        for (int i = 0; i <= settings.MaxLevels; i++)
        {
            if (i > 0)
            {
                // Calculate step
                decimal stepMultiplier = (decimal)Math.Pow((double)settings.StepScale, i - 1);
                decimal stepPercent = settings.BaseStepPercent * stepMultiplier;
                currentDistancePercent += stepPercent;
                
                // For Long, price goes down
                decimal rawPrice = firstOrderPrice.Value * (1m - currentDistancePercent / 100m);
                currentPrice = new Price(rawPrice);
                
                // Calculate volume
                decimal volMultiplier = (decimal)Math.Pow((double)settings.VolumeScale, i);
                currentVolume = new Money(settings.FirstOrderVolume.Value * volMultiplier);
            }

            var roundedPrice = rules.RoundPrice(currentPrice);
            
            // Validate overlapping levels
            if (i > 0 && roundedPrice.Value >= levels[^1].Price.Value)
            {
                throw new InvalidOperationException($"Grid calculation failed: Level {i} price ({roundedPrice.Value}) overlapped or is higher than previous level.");
            }

            // Calculate quantity based on Money and roundedPrice
            var rawQty = new Quantity(currentVolume.Value / roundedPrice.Value);
            var roundedQty = rules.RoundQuantityDown(rawQty);
            
            // Recalculate actual money used
            var actualMoney = roundedQty.Value * roundedPrice.Value;
            
            // Validate MinNotional
            if (actualMoney < rules.MinNotional)
            {
                throw new InvalidOperationException($"Grid calculation failed: Level {i} notional ({actualMoney}) is below MinNotional ({rules.MinNotional}).");
            }
            if (roundedQty.Value == 0)
            {
                throw new InvalidOperationException($"Grid calculation failed: Level {i} quantity rounded to zero.");
            }

            // Validate MaxCapital
            totalCapitalUsed = totalCapitalUsed + new Money(actualMoney);
            if (totalCapitalUsed.Value > settings.MaxCapital.Value)
            {
                throw new InvalidOperationException($"Grid calculation failed: Total capital ({totalCapitalUsed.Value}) exceeds MaxCapital ({settings.MaxCapital.Value}).");
            }

            levels.Add(new GridLevel(i, roundedPrice, roundedQty));
        }

        return new GridPlan(levels);
    }
}

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\Domain\GridPlan.cs
```csharp
using System.Collections.Generic;

namespace DCAnt2.Core.Domain;

public readonly record struct GridLevel(int Index, Price Price, Quantity Quantity);

public record GridPlan
{
    public IReadOnlyList<GridLevel> Levels { get; }

    public GridPlan(IReadOnlyList<GridLevel> levels)
    {
        Levels = levels;
    }
}

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\Domain\GridSettings.cs
```csharp
using System;

namespace DCAnt2.Core.Domain;

public record GridSettings
{
    public Money FirstOrderVolume { get; }
    public Money MaxCapital { get; }
    public int MaxLevels { get; }
    public decimal BaseStepPercent { get; }
    public decimal StepScale { get; }
    public decimal VolumeScale { get; }

    public GridSettings(Money firstOrderVolume, Money maxCapital, int maxLevels, decimal baseStepPercent, decimal stepScale, decimal volumeScale)
    {
        if (firstOrderVolume.Value <= 0) throw new ArgumentException("FirstOrderVolume must be positive.");
        if (maxCapital.Value <= 0) throw new ArgumentException("MaxCapital must be positive.");
        if (maxLevels < 0) throw new ArgumentException("MaxLevels cannot be negative.");
        if (baseStepPercent <= 0) throw new ArgumentException("BaseStepPercent must be positive.");
        if (stepScale <= 0) throw new ArgumentException("StepScale must be positive.");
        if (volumeScale <= 0) throw new ArgumentException("VolumeScale must be positive.");

        FirstOrderVolume = firstOrderVolume;
        MaxCapital = maxCapital;
        MaxLevels = maxLevels;
        BaseStepPercent = baseStepPercent;
        StepScale = stepScale;
        VolumeScale = volumeScale;
    }
}

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\Domain\InstrumentRules.cs
```csharp
using System;

namespace DCAnt2.Core.Domain;

public record InstrumentRules
{
    public string QuoteCurrency { get; }
    public decimal TickSize { get; }
    public decimal QuantityStep { get; }
    public decimal MinNotional { get; }

    public InstrumentRules(string quoteCurrency, decimal tickSize, decimal quantityStep, decimal minNotional)
    {
        if (string.IsNullOrWhiteSpace(quoteCurrency)) throw new ArgumentException("Quote currency must be provided.");
        if (tickSize <= 0) throw new ArgumentOutOfRangeException(nameof(tickSize), "Tick size must be greater than zero.");
        if (quantityStep <= 0) throw new ArgumentOutOfRangeException(nameof(quantityStep), "Quantity step must be greater than zero.");
        if (minNotional < 0) throw new ArgumentOutOfRangeException(nameof(minNotional), "Min notional cannot be negative.");

        QuoteCurrency = quoteCurrency;
        TickSize = tickSize;
        QuantityStep = quantityStep;
        MinNotional = minNotional;
    }

    public Price RoundPrice(Price price)
    {
        if (price.Value == 0) return price;
        decimal roundedValue = Math.Round(price.Value / TickSize, MidpointRounding.AwayFromZero) * TickSize;
        return new Price(roundedValue);
    }

    public Quantity RoundQuantityDown(Quantity qty)
    {
        if (qty.Value == 0) return qty;
        decimal roundedValue = Math.Floor(qty.Value / QuantityStep) * QuantityStep;
        return new Quantity(roundedValue);
    }
}

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\Domain\OrderSide.cs
```csharp
namespace DCAnt2.Core.Domain;

public enum OrderSide
{
    Buy,
    Sell
}

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\Domain\TradeCycle.cs
```csharp
using System;
using System.Collections.Generic;

namespace DCAnt2.Core.Domain;

public enum TradeCycleStatus
{
    Active,
    DcaPaused,
    ExitOnly,
    Completed
}

public class TradeCycle
{
    public TradeCycleId Id { get; }
    public TradeCycleStatus Status { get; private set; }
    public Quantity PositionQuantity { get; private set; }
    public Price PositionVwap { get; private set; }
    
    // Tracks active orders internally to map execution events to purposes
    private readonly Dictionary<InternalOrderId, OrderPurpose> _activeOrders = new();
    
    // Track current TP so we can cancel it
    private InternalOrderId? _currentTpId;
    
    private readonly decimal _tpPercent;
    private readonly InstrumentRules _rules;
    private readonly OrderSide _cycleSide;

    private readonly List<TradeIntent> _outbox = new();
    public IReadOnlyList<TradeIntent> Outbox => _outbox;
    
    public void ClearOutbox() => _outbox.Clear();

    public TradeCycle(TradeCycleId id, decimal tpPercent, InstrumentRules rules, OrderSide cycleSide)
    {
        if (tpPercent <= 0) throw new ArgumentException("Take profit percentage must be positive.");
        
        Id = id;
        Status = TradeCycleStatus.Active;
        PositionQuantity = Quantity.Zero;
        PositionVwap = Price.Zero;
        _tpPercent = tpPercent;
        _rules = rules;
        _cycleSide = cycleSide;
    }
    
    public void Start(Price price, Quantity quantity)
    {
        if (Status != TradeCycleStatus.Active) return;
        
        var id = InternalOrderId.New();
        _activeOrders[id] = OrderPurpose.FirstOrder;
        _outbox.Add(new PlaceOrderIntent(id, OrderPurpose.FirstOrder, _cycleSide, price, quantity));
    }
    
    public void PlaceDca(Price price, Quantity quantity)
    {
        if (Status != TradeCycleStatus.Active) return;
        
        var id = InternalOrderId.New();
        _activeOrders[id] = OrderPurpose.DcaOrder;
        _outbox.Add(new PlaceOrderIntent(id, OrderPurpose.DcaOrder, _cycleSide, price, quantity));
    }

    public void Handle(OrderExecuted evt)
    {
        if (!_activeOrders.TryGetValue(evt.OrderId, out var purpose))
            return; // Order doesn't belong to this cycle or was already handled
            
        _activeOrders.Remove(evt.OrderId);
        
        if (purpose == OrderPurpose.TakeProfit || purpose == OrderPurpose.StopLoss)
        {
            PositionQuantity = Quantity.Zero;
            Status = TradeCycleStatus.Completed;
            return;
        }

        // It's a FirstOrder or DcaOrder -> Update Position and VWAP
        decimal currentTotalValue = PositionQuantity.Value * PositionVwap.Value;
        decimal executionValue = evt.ExecutedQuantity.Value * evt.ExecutedPrice.Value;
        
        decimal newQuantityValue = PositionQuantity.Value + evt.ExecutedQuantity.Value;
        
        PositionQuantity = new Quantity(newQuantityValue);
        PositionVwap = new Price((currentTotalValue + executionValue) / newQuantityValue);

        ReplaceTakeProfit();
    }

    public void Handle(OrderRejected evt)
    {
        if (!_activeOrders.TryGetValue(evt.OrderId, out var purpose))
            return;
            
        _activeOrders.Remove(evt.OrderId);

        // If a DCA or First order fails, we cannot continue grid, must enter ExitOnly
        if (purpose == OrderPurpose.DcaOrder || purpose == OrderPurpose.FirstOrder)
        {
            Status = TradeCycleStatus.ExitOnly;
        }
    }

    private void ReplaceTakeProfit()
    {
        if (_currentTpId.HasValue)
        {
            _outbox.Add(new CancelOrderIntent(_currentTpId.Value));
            _activeOrders.Remove(_currentTpId.Value);
        }

        decimal tpPriceRaw = _cycleSide == OrderSide.Buy 
            ? PositionVwap.Value * (1 + _tpPercent / 100m)
            : PositionVwap.Value * (1 - _tpPercent / 100m);

        Price tpPrice = _rules.RoundPrice(new Price(tpPriceRaw));
        OrderSide tpSide = _cycleSide == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
        
        _currentTpId = InternalOrderId.New();
        _activeOrders[_currentTpId.Value] = OrderPurpose.TakeProfit;
        
        _outbox.Add(new PlaceOrderIntent(_currentTpId.Value, OrderPurpose.TakeProfit, tpSide, tpPrice, PositionQuantity));
    }
}

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\Domain\TradeEvents.cs
```csharp
namespace DCAnt2.Core.Domain;

public abstract record TradeEvent;

public record OrderExecuted(
    InternalOrderId OrderId, 
    Price ExecutedPrice, 
    Quantity ExecutedQuantity) : TradeEvent;

public record OrderRejected(
    InternalOrderId OrderId, 
    string Reason) : TradeEvent;

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\Domain\TradeIntents.cs
```csharp
namespace DCAnt2.Core.Domain;

public abstract record TradeIntent;

public enum OrderPurpose 
{ 
    FirstOrder, 
    DcaOrder, 
    TakeProfit, 
    StopLoss 
}

public record PlaceOrderIntent(
    InternalOrderId OrderId, 
    OrderPurpose Purpose, 
    OrderSide Side, 
    Price Price, 
    Quantity Quantity) : TradeIntent;

public record CancelOrderIntent(
    InternalOrderId OrderId) : TradeIntent;

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\Domain\TradingIds.cs
```csharp
namespace DCAnt2.Core.Domain;

public readonly record struct StrategyInstanceId(string Value)
{
    public static StrategyInstanceId New() => new(IdGenerator.GenerateWithPrefix("strat"));
    public override string ToString() => Value;
}

public readonly record struct TradeCycleId(string Value)
{
    public static TradeCycleId New() => new(IdGenerator.GenerateWithPrefix("cyc"));
    public override string ToString() => Value;
}

public readonly record struct InternalOrderId(string Value)
{
    public static InternalOrderId New() => new(IdGenerator.GenerateWithPrefix("ord"));
    public override string ToString() => Value;
}

public readonly record struct EffectId(string Value)
{
    public static EffectId New() => new(IdGenerator.GenerateWithPrefix("eff"));
    public override string ToString() => Value;
}

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\DCAnt2.Core.csproj
```csharp
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\IdGenerator.cs
```csharp
using System;

namespace DCAnt2.Core;

public static class IdGenerator
{
    public static string GenerateWithPrefix(string prefix)
    {
        var datePart = DateTime.UtcNow.ToString("ddMMyy");
        var hexPart = Guid.NewGuid().ToString("N").Substring(0, 12);
        return $"{prefix}_{datePart}_{hexPart}";
    }
}

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Infrastructure\DCAnt2.Infrastructure.csproj
```csharp
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\DCAnt2.Core\DCAnt2.Core.csproj" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Plugin\DCAnt2.Plugin.csproj
```csharp
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\DCAnt2.Core\DCAnt2.Core.csproj" />
    <ProjectReference Include="..\DCAnt2.Infrastructure\DCAnt2.Infrastructure.csproj" />
    <ProjectReference Include="..\DCAnt2.Quantower\DCAnt2.Quantower.csproj" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Quantower\DCAnt2.Quantower.csproj
```csharp
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\DCAnt2.Core\DCAnt2.Core.csproj" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\tests\DCAnt2.QuantowerTests\DCAnt2.QuantowerTests.csproj
```csharp
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\DCAnt2.Core\DCAnt2.Core.csproj" />
    <ProjectReference Include="..\..\src\DCAnt2.Infrastructure\DCAnt2.Infrastructure.csproj" />
    <ProjectReference Include="..\..\src\DCAnt2.Quantower\DCAnt2.Quantower.csproj" />
    <ProjectReference Include="..\..\src\DCAnt2.Plugin\DCAnt2.Plugin.csproj" />
  </ItemGroup>

</Project>
```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\tests\DCAnt2.QuantowerTests\UnitTest1.cs
```csharp
namespace DCAnt2.QuantowerTests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {

    }
}

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\tests\DCAnt2.Tests\DCAnt2.Tests.csproj
```csharp
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\DCAnt2.Core\DCAnt2.Core.csproj" />
    <ProjectReference Include="..\..\src\DCAnt2.Infrastructure\DCAnt2.Infrastructure.csproj" />
    <ProjectReference Include="..\..\src\DCAnt2.Quantower\DCAnt2.Quantower.csproj" />
    <ProjectReference Include="..\..\src\DCAnt2.Plugin\DCAnt2.Plugin.csproj" />
  </ItemGroup>

</Project>
```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\tests\DCAnt2.Tests\FinancialTypesTests.cs
```csharp
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

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\tests\DCAnt2.Tests\GridCalculatorTests.cs
```csharp
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

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\tests\DCAnt2.Tests\IdGeneratorTests.cs
```csharp
using System;
using System.Collections.Generic;
using DCAnt2.Core;
using Xunit;

namespace DCAnt2.Tests;

public class IdGeneratorTests
{
    [Fact]
    public void GeneratedId_HasCorrectPrefixAndFormat()
    {
        var id = IdGenerator.GenerateWithPrefix("ord");
        
        Assert.StartsWith("ord_", id);
        
        var parts = id.Split('_');
        Assert.Equal(3, parts.Length);
        Assert.Equal("ord", parts[0]);
        Assert.Equal(6, parts[1].Length);
        Assert.Equal(12, parts[2].Length);
        Assert.Equal(23, id.Length);
    }

    [Fact]
    public void GeneratedIds_AreUnique()
    {
        var ids = new HashSet<string>();
        for (int i = 0; i < 1000; i++)
        {
            var id = IdGenerator.GenerateWithPrefix("ord");
            Assert.DoesNotContain(id, ids);
            ids.Add(id);
        }
    }
}

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\tests\DCAnt2.Tests\InstrumentRulesTests.cs
```csharp
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

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\tests\DCAnt2.Tests\TradeCycleTests.cs
```csharp
using DCAnt2.Core.Domain;
using Xunit;
using System.Linq;

namespace DCAnt2.Tests;

public class TradeCycleTests
{
    private readonly InstrumentRules _rules = new InstrumentRules("USDT", 0.01m, 0.01m, 1m);

    [Fact]
    public void Start_CreatesFirstOrderIntent()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), 2m, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));

        Assert.Single(cycle.Outbox);
        var intent = (PlaceOrderIntent)cycle.Outbox[0];
        Assert.NotNull(intent);
        Assert.Equal(OrderPurpose.FirstOrder, intent.Purpose);
        Assert.Equal(OrderSide.Buy, intent.Side);
        Assert.Equal(100m, intent.Price.Value);
        Assert.Equal(1m, intent.Quantity.Value);
        Assert.Equal(TradeCycleStatus.Active, cycle.Status);
    }

    [Fact]
    public void Handle_FirstOrderExecuted_UpdatesPositionAndPlacesTakeProfit()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), 2m, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));
        
        var startIntent = (PlaceOrderIntent)cycle.Outbox[0];
        cycle.ClearOutbox();

        cycle.Handle(new OrderExecuted(startIntent.OrderId, new Price(100m), new Quantity(1m)));

        Assert.Equal(1m, cycle.PositionQuantity.Value);
        Assert.Equal(100m, cycle.PositionVwap.Value);
        
        Assert.Single(cycle.Outbox);
        var tpIntent = (PlaceOrderIntent)cycle.Outbox[0];
        Assert.NotNull(tpIntent);
        Assert.Equal(OrderPurpose.TakeProfit, tpIntent.Purpose);
        Assert.Equal(OrderSide.Sell, tpIntent.Side); // TP for Buy is Sell
        Assert.Equal(102m, tpIntent.Price.Value); // 100 + 2%
        Assert.Equal(1m, tpIntent.Quantity.Value);
    }

    [Fact]
    public void Handle_DcaOrderExecuted_UpdatesVwapAndMovesTakeProfit()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), 2m, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));
        var startIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single();
        cycle.Handle(new OrderExecuted(startIntent.OrderId, new Price(100m), new Quantity(1m)));
        cycle.ClearOutbox();

        cycle.PlaceDca(new Price(90m), new Quantity(1m));
        var dcaIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single();
        cycle.ClearOutbox();

        cycle.Handle(new OrderExecuted(dcaIntent.OrderId, new Price(90m), new Quantity(1m)));

        Assert.Equal(2m, cycle.PositionQuantity.Value);
        Assert.Equal(95m, cycle.PositionVwap.Value); // (100*1 + 90*1)/2
        
        // Outbox should contain Cancel TP and new Place TP
        Assert.Equal(2, cycle.Outbox.Count);
        Assert.IsType<CancelOrderIntent>(cycle.Outbox[0]);
        var tpIntent = (PlaceOrderIntent)cycle.Outbox[1];
        Assert.NotNull(tpIntent);
        Assert.Equal(OrderPurpose.TakeProfit, tpIntent.Purpose);
        // TP price = 95 + 2% = 96.90
        Assert.Equal(96.90m, tpIntent.Price.Value);
        Assert.Equal(2m, tpIntent.Quantity.Value);
    }

    [Fact]
    public void Handle_OrderRejected_EntersExitOnly()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), 2m, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));
        var startIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single();
        cycle.Handle(new OrderExecuted(startIntent.OrderId, new Price(100m), new Quantity(1m)));
        cycle.ClearOutbox();

        cycle.PlaceDca(new Price(90m), new Quantity(1m));
        var dcaIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single();
        
        cycle.Handle(new OrderRejected(dcaIntent.OrderId, "Insufficient funds"));

        Assert.Equal(TradeCycleStatus.ExitOnly, cycle.Status);
    }

    [Fact]
    public void PlaceDca_WhenExitOnly_DoesNothing()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), 2m, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));
        var startIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single();
        cycle.Handle(new OrderExecuted(startIntent.OrderId, new Price(100m), new Quantity(1m)));
        cycle.ClearOutbox();

        cycle.PlaceDca(new Price(90m), new Quantity(1m));
        var dcaIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single();
        cycle.Handle(new OrderRejected(dcaIntent.OrderId, "Insufficient funds"));
        cycle.ClearOutbox();

        // Already in ExitOnly, shouldn't place DCA
        cycle.PlaceDca(new Price(80m), new Quantity(2m));
        
        Assert.Empty(cycle.Outbox);
    }

    [Fact]
    public void Handle_TakeProfitExecuted_CompletesCycle()
    {
        var cycle = new TradeCycle(TradeCycleId.New(), 2m, _rules, OrderSide.Buy);
        cycle.Start(new Price(100m), new Quantity(1m));
        var startIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single();
        cycle.ClearOutbox();
        
        cycle.Handle(new OrderExecuted(startIntent.OrderId, new Price(100m), new Quantity(1m)));
        
        var tpIntent = cycle.Outbox.OfType<PlaceOrderIntent>().Single(x => x.Purpose == OrderPurpose.TakeProfit);
        
        cycle.Handle(new OrderExecuted(tpIntent.OrderId, tpIntent.Price, tpIntent.Quantity));
        
        Assert.Equal(TradeCycleStatus.Completed, cycle.Status);
        Assert.Equal(0m, cycle.PositionQuantity.Value);
    }
}

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\tests\DCAnt2.Tests\UnitTest1.cs
```csharp
namespace DCAnt2.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {

    }
}

```
