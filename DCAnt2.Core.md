# DCAnt2.Core Source Code

## IdGenerator.cs
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

## Domain\FinancialTypes.cs
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
    
    public override string ToString() => Value.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture);
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
    
    public override string ToString() => Value.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture);
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

    public override string ToString() => Value.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct Percentage
{
    public decimal Value { get; }

    public static Percentage Zero => new(0m);
    
    public Percentage(decimal value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Percentage cannot be negative.");
        Value = value;
    }
    
    public override string ToString() => Value.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture) + "%";
}

```

## Domain\GridCalculator.cs
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
                decimal stepPercent = settings.BaseStepPercent.Value * stepMultiplier;
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
            
            if (roundedQty.Value < rules.MinQuantity.Value)
            {
                throw new InvalidOperationException($"Grid calculation failed: Level {i} quantity ({roundedQty.Value}) is below MinQuantity ({rules.MinQuantity.Value}).");
            }

            // Recalculate actual money used
            var actualMoney = new Money(roundedQty.Value * roundedPrice.Value);
            
            // Validate MinNotional
            if (actualMoney.Value < rules.MinNotional)
            {
                throw new InvalidOperationException($"Grid calculation failed: Level {i} notional ({actualMoney.Value}) is below MinNotional ({rules.MinNotional}).");
            }

            // Validate MaxCapital
            totalCapitalUsed = totalCapitalUsed + actualMoney;
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

## Domain\GridPlan.cs
```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace DCAnt2.Core.Domain;

public readonly record struct GridLevel(int Index, Price Price, Quantity Quantity);

public record GridPlan
{
    public IReadOnlyList<GridLevel> Levels { get; }
    public Quantity TotalQuantity { get; }
    public Money TotalNotional { get; }
    public Price ExpectedVwap { get; }

    public GridPlan(IEnumerable<GridLevel> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        
        var array = levels.ToArray();
        if (array.Length == 0)
        {
            throw new ArgumentException("Grid levels cannot be empty.", nameof(levels));
        }

        decimal totalQty = 0m;
        decimal totalNotional = 0m;

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

            totalQty += level.Quantity.Value;
            totalNotional += (level.Price.Value * level.Quantity.Value);
        }

        TotalQuantity = new Quantity(totalQty);
        TotalNotional = new Money(totalNotional);
        ExpectedVwap = new Price(TotalNotional.Value / TotalQuantity.Value);
        Levels = Array.AsReadOnly(array);
    }
}

```

## Domain\GridSettings.cs
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
    public Percentage BaseStepPercent { get; }
    public decimal StepScale { get; }
    public decimal VolumeScale { get; }

    public GridSettings(Money firstOrderVolume, Money maxCapital, int maxLevels, Percentage baseStepPercent, decimal stepScale, decimal volumeScale)
    {
        if (firstOrderVolume.Value <= 0) throw new ArgumentOutOfRangeException(nameof(firstOrderVolume), "FirstOrderVolume must be positive.");
        if (maxCapital.Value <= 0) throw new ArgumentOutOfRangeException(nameof(maxCapital), "MaxCapital must be positive.");
        if (firstOrderVolume.Value > maxCapital.Value) throw new ArgumentOutOfRangeException(nameof(firstOrderVolume), "FirstOrderVolume cannot exceed MaxCapital.");
        if (maxLevels < 0) throw new ArgumentOutOfRangeException(nameof(maxLevels), "MaxLevels cannot be negative.");
        if (maxLevels > 1000) throw new ArgumentOutOfRangeException(nameof(maxLevels), "MaxLevels cannot exceed 1000.");
        if (baseStepPercent.Value <= 0) throw new ArgumentOutOfRangeException(nameof(baseStepPercent), "BaseStepPercent must be positive.");
        if (stepScale <= 0) throw new ArgumentOutOfRangeException(nameof(stepScale), "StepScale must be positive.");
        if (volumeScale <= 0) throw new ArgumentOutOfRangeException(nameof(volumeScale), "VolumeScale must be positive.");

        FirstOrderVolume = firstOrderVolume;
        MaxCapital = maxCapital;
        MaxLevels = maxLevels;
        BaseStepPercent = baseStepPercent;
        StepScale = stepScale;
        VolumeScale = volumeScale;
    }
}

```

## Domain\InstrumentRules.cs
```csharp
using System;

namespace DCAnt2.Core.Domain;

public record InstrumentRules
{
    public string QuoteCurrency { get; }
    public decimal TickSize { get; }
    public decimal QuantityStep { get; }
    public Quantity MinQuantity { get; }
    public decimal MinNotional { get; }

    public InstrumentRules(string quoteCurrency, decimal tickSize, decimal quantityStep, Quantity minQuantity, decimal minNotional)
    {
        if (string.IsNullOrWhiteSpace(quoteCurrency)) throw new ArgumentException("Quote currency must be provided.");
        if (tickSize <= 0) throw new ArgumentOutOfRangeException(nameof(tickSize), "Tick size must be greater than zero.");
        if (quantityStep <= 0) throw new ArgumentOutOfRangeException(nameof(quantityStep), "Quantity step must be greater than zero.");
        if (minQuantity.Value == 0m) throw new ArgumentOutOfRangeException(nameof(minQuantity), "Min quantity must be greater than zero.");
        if (minNotional < 0) throw new ArgumentOutOfRangeException(nameof(minNotional), "Min notional cannot be negative.");

        QuoteCurrency = quoteCurrency;
        TickSize = tickSize;
        QuantityStep = quantityStep;
        MinQuantity = minQuantity;
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

## Domain\OrderPurpose.cs
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

## Domain\OrderSide.cs
```csharp
namespace DCAnt2.Core.Domain;

public enum OrderSide
{
    Buy,
    Sell
}

```

## Domain\TradeCycle.cs
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
    
    private readonly Dictionary<InternalOrderId, OrderPurpose> _registeredOrders = new();
    
    public int RegisteredOrderCount => _registeredOrders.Count;

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

        if (!_registeredOrders.TryAdd(id, purpose))
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
        ArgumentNullException.ThrowIfNull(id);
        return _registeredOrders.ContainsKey(id);
    }

    public bool TryGetOrderPurpose(InternalOrderId id, out OrderPurpose purpose)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _registeredOrders.TryGetValue(id, out purpose);
    }
}

```

## Domain\TradeDirection.cs
```csharp
namespace DCAnt2.Core.Domain;

public enum TradeDirection
{
    Long,
    Short
}

```

## Domain\TradingIds.cs
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

## Engine\EngineLoop.cs
```csharp
using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DCAnt2.Core.Engine;

/// <summary>
/// Изолированный, последовательный цикл обработки сообщений.
/// </summary>
public sealed class EngineLoop : IAsyncDisposable
{
    private const int ChannelCapacity = 1024;
    private readonly object _stateLock = new();
    private readonly Channel<EngineMessage> _channel;
    private readonly Action<EngineMessage> _messageHandler;
    private readonly Action<Exception>? _panicHandler;

    private EngineLoopState _state = EngineLoopState.Created;
    private Task? _loopTask;

    public EngineLoop(Action<EngineMessage> messageHandler, Action<Exception>? panicHandler = null)
    {
        ArgumentNullException.ThrowIfNull(messageHandler);

        _messageHandler = messageHandler;
        _panicHandler = panicHandler;

        _channel = Channel.CreateBounded<EngineMessage>(
            new BoundedChannelOptions(ChannelCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });
    }

    public EngineLoopState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }

    public bool TryEnqueue(EngineMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (_stateLock)
        {
            if (_state != EngineLoopState.Running)
            {
                return false;
            }

            return _channel.Writer.TryWrite(message);
        }
    }

    public void Start()
    {
        lock (_stateLock)
        {
            if (_state != EngineLoopState.Created)
            {
                throw new InvalidOperationException($"Cannot start EngineLoop from state {_state}");
            }

            _state = EngineLoopState.Running;
            _loopTask = Task.Run(ReadAllAsync);
        }
    }

    public async Task StopAsync()
    {
        Task? loopTaskToAwait = null;

        lock (_stateLock)
        {
            switch (_state)
            {
                case EngineLoopState.Created:
                    _channel.Writer.TryComplete();
                    _state = EngineLoopState.Stopped;
                    return;
                case EngineLoopState.Running:
                    _state = EngineLoopState.Stopping;
                    _channel.Writer.TryComplete();
                    loopTaskToAwait = _loopTask;
                    break;
                case EngineLoopState.Stopping:
                case EngineLoopState.Faulted:
                    loopTaskToAwait = _loopTask;
                    break;
                case EngineLoopState.Stopped:
                    return;
            }
        }

        if (loopTaskToAwait is not null)
        {
            await loopTaskToAwait.ConfigureAwait(false);
        }
    }

    private async Task ReadAllAsync()
    {
        try
        {
            await foreach (var message in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                _messageHandler(message);
            }

            lock (_stateLock)
            {
                if (_state == EngineLoopState.Stopping)
                {
                    _state = EngineLoopState.Stopped;
                }
            }
        }
        catch (Exception ex)
        {
            lock (_stateLock)
            {
                _state = EngineLoopState.Faulted;
                _channel.Writer.TryComplete();
            }

            try
            {
                _panicHandler?.Invoke(ex);
            }
            catch
            {
                // Ignore panic handler exception to avoid hanging the task completion
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }
}

```

## Engine\EngineLoopState.cs
```csharp
namespace DCAnt2.Core.Engine;

/// <summary>
/// Состояния жизненного цикла <see cref="EngineLoop"/>.
/// </summary>
public enum EngineLoopState
{
    /// <summary>
    /// Объект создан, но обработка еще не запущена.
    /// </summary>
    Created,

    /// <summary>
    /// Движок принимает и обрабатывает сообщения.
    /// </summary>
    Running,

    /// <summary>
    /// Запрошена остановка. Новые сообщения отклоняются, идет обработка оставшихся в очереди.
    /// </summary>
    Stopping,

    /// <summary>
    /// Очередь исчерпана, обработка штатно завершена.
    /// </summary>
    Stopped,

    /// <summary>
    /// Аварийная остановка из-за исключения.
    /// </summary>
    Faulted
}

```

## Engine\EngineMessage.cs
```csharp
namespace DCAnt2.Core.Engine;

/// <summary>
/// Базовый тип для всех внутренних сообщений, обрабатываемых в <see cref="EngineLoop"/>.
/// </summary>
public abstract record EngineMessage;

```

