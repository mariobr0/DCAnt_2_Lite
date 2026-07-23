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

public readonly record struct ClientRequestId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct BrokerOrderId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct ExecutionId(string Value)
{
    public override string ToString() => Value;
}
