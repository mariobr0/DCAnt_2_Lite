namespace DCAnt2.Core.Domain;

public readonly record struct Percentage(decimal Value)
{
    public static implicit operator decimal(Percentage p) => p.Value;
    public static explicit operator Percentage(decimal d) => new(d);
    
    public override string ToString() => $"{Value}%";
}
