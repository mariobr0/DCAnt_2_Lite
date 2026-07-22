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
