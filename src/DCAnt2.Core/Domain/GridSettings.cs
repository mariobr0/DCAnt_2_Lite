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
