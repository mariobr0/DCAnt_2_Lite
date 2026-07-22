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
