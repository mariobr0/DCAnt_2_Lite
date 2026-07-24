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
