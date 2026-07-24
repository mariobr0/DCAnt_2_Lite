# Code Dump: 8c65c16

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
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(rules);

        if (firstOrderPrice.Value <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(firstOrderPrice), "First order price must be positive.");
        }

        var levels = new List<GridLevel>(settings.MaxLevels + 1);
        
        var currentPrice = firstOrderPrice;
        decimal currentDistancePercent = 0;
        
        var currentVolume = settings.FirstOrderVolume;
        Money totalCapitalUsed = Money.Zero;

        for (int i = 0; i <= settings.MaxLevels; i++)
        {
            // Level 0 = First Order, Levels 1..MaxLevels = DCA Orders
            if (i > 0)
            {
                // Calculate step
                decimal stepMultiplier = Pow(settings.StepScale, i - 1);
                decimal stepPercent = settings.BaseStepPercent * stepMultiplier;
                currentDistancePercent += stepPercent;
                
                // For Long, price goes down
                decimal rawPrice = firstOrderPrice.Value * (1m - currentDistancePercent / 100m);
                
                if (rawPrice <= 0m)
                {
                    throw new InvalidOperationException($"Grid calculation failed: Level {i} produced non-positive price {rawPrice}. Cumulative distance is {currentDistancePercent}%.");
                }
                
                currentPrice = new Price(rawPrice);
                
                // Calculate volume
                decimal volMultiplier = Pow(settings.VolumeScale, i);
                currentVolume = new Money(settings.FirstOrderVolume.Value * volMultiplier);
            }

            var roundedPrice = rules.RoundPriceToNearest(currentPrice);
            
            if (roundedPrice.Value <= 0m)
            {
                throw new InvalidOperationException($"Grid calculation failed: Level {i} price rounded to a non-positive value. Raw price: {currentPrice.Value}, Tick size: {rules.TickSize}.");
            }
            
            // Validate overlapping levels
            if (i > 0 && roundedPrice.Value >= levels[^1].Price.Value)
            {
                throw new InvalidOperationException($"Grid calculation failed: Level {i} price ({roundedPrice.Value}) overlapped or is higher than previous level.");
            }

            // Calculate quantity based on Money and roundedPrice
            var rawQty = new Quantity(currentVolume.Value / roundedPrice.Value);
            var roundedQty = rules.RoundQuantityDown(rawQty);
            
            if (roundedQty.Value == 0m)
            {
                throw new InvalidOperationException($"Grid calculation failed: Level {i} quantity rounded to zero.");
            }
            
            // Recalculate actual money used
            var actualMoney = roundedQty.Value * roundedPrice.Value;
            
            // Validate MinNotional
            if (actualMoney < rules.MinNotional)
            {
                throw new InvalidOperationException($"Grid calculation failed: Level {i} notional ({actualMoney}) is below MinNotional ({rules.MinNotional}).");
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

    private static decimal Pow(decimal value, int exponent)
    {
        if (exponent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exponent), "Exponent cannot be negative.");
        }

        var result = 1m;

        checked
        {
            for (var i = 0; i < exponent; i++)
            {
                result *= value;
            }
        }

        return result;
    }
}

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\Domain\GridPlan.cs

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace DCAnt2.Core.Domain;

public readonly record struct GridLevel(int Index, Price Price, Quantity Quantity);

public record GridPlan
{
    public IReadOnlyList<GridLevel> Levels { get; }

    public GridPlan(IEnumerable<GridLevel> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        
        var array = levels.ToArray();
        if (array.Length == 0)
        {
            throw new ArgumentException("Grid levels cannot be empty.", nameof(levels));
        }

        for (int i = 0; i < array.Length; i++)
        {
            var level = array[i];
            
            if (level.Index != i)
            {
                throw new ArgumentException($"Grid level at position {i} must have index {i}, but has index {level.Index}.", nameof(levels));
            }
            
            if (level.Price.Value <= 0m)
            {
                throw new ArgumentException($"Grid level {i} must have a positive price.", nameof(levels));
            }
            
            if (level.Quantity.Value <= 0m)
            {
                throw new ArgumentException($"Grid level {i} must have a positive quantity.", nameof(levels));
            }
        }

        Levels = Array.AsReadOnly(array);
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

    /// <summary>
    /// Number of DCA levels excluding the first order.
    /// A value of zero produces a plan containing only the first order.
    /// </summary>
    public int MaxLevels { get; }
    public decimal BaseStepPercent { get; }
    public decimal StepScale { get; }
    public decimal VolumeScale { get; }

    public GridSettings(Money firstOrderVolume, Money maxCapital, int maxLevels, decimal baseStepPercent, decimal stepScale, decimal volumeScale)
    {
        if (firstOrderVolume.Value <= 0) throw new ArgumentException("FirstOrderVolume must be positive.");
        if (maxCapital.Value <= 0) throw new ArgumentException("MaxCapital must be positive.");
        if (firstOrderVolume.Value > maxCapital.Value) throw new ArgumentException("FirstOrderVolume cannot exceed MaxCapital.");
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

    public Price RoundPriceToNearest(Price price)
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
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\Domain\OrderPurpose.cs

```csharp
namespace DCAnt2.Core.Domain;

public enum OrderPurpose
{
    FirstOrder,
    DcaOrder,
    TakeProfit,
    StopLoss
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
    public TradeDirection Direction { get; }
    public TradeCycleStatus Status { get; private set; }
    public Quantity PositionQuantity { get; private set; }
    public Price PositionVwap { get; private set; }
    
    private readonly Dictionary<InternalOrderId, OrderPurpose> _activeOrders = new();
    
    public int RegisteredOrderCount => _activeOrders.Count;

    public TradeCycle(TradeCycleId id, TradeDirection direction)
    {
        ArgumentNullException.ThrowIfNull(id);

        Id = id;
        Direction = direction;
        Status = TradeCycleStatus.Active;
        PositionQuantity = Quantity.Zero;
        PositionVwap = Price.Zero;
    }
    
    public void RegisterOrder(InternalOrderId id, OrderPurpose purpose)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (!_activeOrders.TryAdd(id, purpose))
        {
            throw new InvalidOperationException($"Order {id} is already registered.");
        }
    }

    public void UpdatePositionSnapshot(Quantity quantity, Price vwap)
    {
        if (quantity.Value == 0m && vwap.Value != 0m)
        {
            throw new ArgumentException("VWAP must be zero when position is empty.");
        }

        if (quantity.Value > 0m && vwap.Value <= 0m)
        {
            throw new ArgumentException("VWAP must be positive for an open position.");
        }

        PositionQuantity = quantity;
        PositionVwap = vwap;
    }

    public void PauseDca()
    {
        if (Status == TradeCycleStatus.Active)
        {
            Status = TradeCycleStatus.DcaPaused;
        }
    }

    public void EnterExitOnly()
    {
        if (Status == TradeCycleStatus.Completed)
        {
            return;
        }

        Status = TradeCycleStatus.ExitOnly;
    }

    public bool OwnsOrder(InternalOrderId id)
    {
        return _activeOrders.ContainsKey(id);
    }

    public bool TryGetOrderPurpose(InternalOrderId id, out OrderPurpose purpose)
    {
        return _activeOrders.TryGetValue(id, out purpose);
    }
}

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\Domain\TradeDirection.cs

```csharp
namespace DCAnt2.Core.Domain;

public enum TradeDirection
{
    Long,
    Short
}

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\Domain\TradingIds.cs

```csharp
using System;

namespace DCAnt2.Core.Domain;

public sealed record StrategyInstanceId
{
    public string Value { get; }

    public StrategyInstanceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Strategy instance ID must be provided.", nameof(value));
        }

        Value = value;
    }

    public static StrategyInstanceId New()
    {
        return new StrategyInstanceId(IdGenerator.GenerateWithPrefix("strat"));
    }

    public override string ToString() => Value;
}

public sealed record TradeCycleId
{
    public string Value { get; }

    public TradeCycleId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Trade cycle ID must be provided.", nameof(value));
        }

        Value = value;
    }

    public static TradeCycleId New()
    {
        return new TradeCycleId(IdGenerator.GenerateWithPrefix("cyc"));
    }

    public override string ToString() => Value;
}

public sealed record InternalOrderId
{
    public string Value { get; }

    public InternalOrderId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Internal order ID must be provided.", nameof(value));
        }

        Value = value;
    }

    public static InternalOrderId New()
    {
        return new InternalOrderId(IdGenerator.GenerateWithPrefix("ord"));
    }

    public override string ToString() => Value;
}

public sealed record EffectId
{
    public string Value { get; }

    public EffectId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Effect ID must be provided.", nameof(value));
        }

        Value = value;
    }

    public static EffectId New()
    {
        return new EffectId(IdGenerator.GenerateWithPrefix("eff"));
    }

    public override string ToString() => Value;
}

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs

```csharp
// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\obj\Debug\net10.0\DCAnt2.Core.AssemblyInfo.cs

```csharp
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("DCAnt2.Core")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+d59da2b93b24f0ae80257674f3103603711a0696")]
[assembly: System.Reflection.AssemblyProductAttribute("DCAnt2.Core")]
[assembly: System.Reflection.AssemblyTitleAttribute("DCAnt2.Core")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// Generated by the MSBuild WriteCodeFragment class.


```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\obj\Debug\net10.0\DCAnt2.Core.GlobalUsings.g.cs

```csharp
// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\obj\Release\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs

```csharp
// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\obj\Release\net10.0\DCAnt2.Core.AssemblyInfo.cs

```csharp
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("DCAnt2.Core")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Release")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+8c65c1667a64a8644424626516c33ed961f6dbd9")]
[assembly: System.Reflection.AssemblyProductAttribute("DCAnt2.Core")]
[assembly: System.Reflection.AssemblyTitleAttribute("DCAnt2.Core")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// Generated by the MSBuild WriteCodeFragment class.


```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Core\obj\Release\net10.0\DCAnt2.Core.GlobalUsings.g.cs

```csharp
// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;

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
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("Prefix must be provided.", nameof(prefix));
        }

        var datePart = DateTime.UtcNow.ToString("ddMMyy");
        var hexPart = Guid.NewGuid().ToString("N")[..12];
        return $"{prefix}_{datePart}_{hexPart}";
    }
}

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Infrastructure\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs

```csharp
// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Infrastructure\obj\Debug\net10.0\DCAnt2.Infrastructure.AssemblyInfo.cs

```csharp
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("DCAnt2.Infrastructure")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+d59da2b93b24f0ae80257674f3103603711a0696")]
[assembly: System.Reflection.AssemblyProductAttribute("DCAnt2.Infrastructure")]
[assembly: System.Reflection.AssemblyTitleAttribute("DCAnt2.Infrastructure")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// Generated by the MSBuild WriteCodeFragment class.


```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Infrastructure\obj\Debug\net10.0\DCAnt2.Infrastructure.GlobalUsings.g.cs

```csharp
// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Infrastructure\obj\Release\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs

```csharp
// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Infrastructure\obj\Release\net10.0\DCAnt2.Infrastructure.AssemblyInfo.cs

```csharp
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("DCAnt2.Infrastructure")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Release")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+8c65c1667a64a8644424626516c33ed961f6dbd9")]
[assembly: System.Reflection.AssemblyProductAttribute("DCAnt2.Infrastructure")]
[assembly: System.Reflection.AssemblyTitleAttribute("DCAnt2.Infrastructure")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// Generated by the MSBuild WriteCodeFragment class.


```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Infrastructure\obj\Release\net10.0\DCAnt2.Infrastructure.GlobalUsings.g.cs

```csharp
// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;

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
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Plugin\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs

```csharp
// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Plugin\obj\Debug\net10.0\DCAnt2.Plugin.AssemblyInfo.cs

```csharp
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("DCAnt2.Plugin")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+d59da2b93b24f0ae80257674f3103603711a0696")]
[assembly: System.Reflection.AssemblyProductAttribute("DCAnt2.Plugin")]
[assembly: System.Reflection.AssemblyTitleAttribute("DCAnt2.Plugin")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// Generated by the MSBuild WriteCodeFragment class.


```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Plugin\obj\Debug\net10.0\DCAnt2.Plugin.GlobalUsings.g.cs

```csharp
// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Plugin\obj\Release\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs

```csharp
// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Plugin\obj\Release\net10.0\DCAnt2.Plugin.AssemblyInfo.cs

```csharp
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("DCAnt2.Plugin")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Release")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+8c65c1667a64a8644424626516c33ed961f6dbd9")]
[assembly: System.Reflection.AssemblyProductAttribute("DCAnt2.Plugin")]
[assembly: System.Reflection.AssemblyTitleAttribute("DCAnt2.Plugin")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// Generated by the MSBuild WriteCodeFragment class.


```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Plugin\obj\Release\net10.0\DCAnt2.Plugin.GlobalUsings.g.cs

```csharp
// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;

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
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Quantower\obj\Debug\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs

```csharp
// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Quantower\obj\Debug\net10.0\DCAnt2.Quantower.AssemblyInfo.cs

```csharp
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("DCAnt2.Quantower")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+d59da2b93b24f0ae80257674f3103603711a0696")]
[assembly: System.Reflection.AssemblyProductAttribute("DCAnt2.Quantower")]
[assembly: System.Reflection.AssemblyTitleAttribute("DCAnt2.Quantower")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// Generated by the MSBuild WriteCodeFragment class.


```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Quantower\obj\Debug\net10.0\DCAnt2.Quantower.GlobalUsings.g.cs

```csharp
// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Quantower\obj\Release\net10.0\.NETCoreApp,Version=v10.0.AssemblyAttributes.cs

```csharp
// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]

```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Quantower\obj\Release\net10.0\DCAnt2.Quantower.AssemblyInfo.cs

```csharp
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("DCAnt2.Quantower")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Release")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+8c65c1667a64a8644424626516c33ed961f6dbd9")]
[assembly: System.Reflection.AssemblyProductAttribute("DCAnt2.Quantower")]
[assembly: System.Reflection.AssemblyTitleAttribute("DCAnt2.Quantower")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// Generated by the MSBuild WriteCodeFragment class.


```
## C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\src\DCAnt2.Quantower\obj\Release\net10.0\DCAnt2.Quantower.GlobalUsings.g.cs

```csharp
// <auto-generated/>
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;

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
