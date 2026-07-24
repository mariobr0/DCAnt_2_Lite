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
