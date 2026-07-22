using System;
using System.Collections.Generic;

namespace DCAnt2.Core.Domain;

public static class GridCalculator
{
    public static GridPlan Calculate(GridSettings settings, Price firstOrderPrice, InstrumentRules rules)
    {
        var levels = new List<GridLevel>(settings.MaxLevels + 1);
        
        var currentPrice = firstOrderPrice;
        decimal currentDistancePercent = 0;
        
        var currentVolume = settings.FirstOrderVolume;
        Money totalCapitalUsed = Money.Zero;

        for (int i = 0; i <= settings.MaxLevels; i++)
        {
            if (i > 0)
            {
                // Calculate step
                decimal stepMultiplier = (decimal)Math.Pow((double)settings.StepScale, i - 1);
                decimal stepPercent = settings.BaseStepPercent * stepMultiplier;
                currentDistancePercent += stepPercent;
                
                // For Long, price goes down
                decimal rawPrice = firstOrderPrice.Value * (1m - currentDistancePercent / 100m);
                currentPrice = new Price(rawPrice);
                
                // Calculate volume
                decimal volMultiplier = (decimal)Math.Pow((double)settings.VolumeScale, i);
                currentVolume = new Money(settings.FirstOrderVolume.Value * volMultiplier);
            }

            var roundedPrice = rules.RoundPrice(currentPrice);
            
            // Validate overlapping levels
            if (i > 0 && roundedPrice.Value >= levels[^1].Price.Value)
            {
                throw new InvalidOperationException($"Grid calculation failed: Level {i} price ({roundedPrice.Value}) overlapped or is higher than previous level.");
            }

            // Calculate quantity based on Money and roundedPrice
            var rawQty = new Quantity(currentVolume.Value / roundedPrice.Value);
            var roundedQty = rules.RoundQuantityDown(rawQty);
            
            // Recalculate actual money used
            var actualMoney = roundedQty.Value * roundedPrice.Value;
            
            // Validate MinNotional
            if (actualMoney < rules.MinNotional)
            {
                throw new InvalidOperationException($"Grid calculation failed: Level {i} notional ({actualMoney}) is below MinNotional ({rules.MinNotional}).");
            }
            if (roundedQty.Value == 0)
            {
                throw new InvalidOperationException($"Grid calculation failed: Level {i} quantity rounded to zero.");
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
}
