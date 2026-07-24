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
