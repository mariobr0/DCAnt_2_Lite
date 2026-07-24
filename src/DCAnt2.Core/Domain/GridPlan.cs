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
