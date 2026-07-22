using System;

namespace DCAnt2.Core.Domain;

public record GridSettings
{
    public Money FirstOrderVolume { get; }
    public Money MaxCapital { get; }
    public int MaxLevels { get; }
    public decimal BaseStepPercent { get; }
    public decimal StepScale { get; }
    public decimal VolumeScale { get; }

    public GridSettings(Money firstOrderVolume, Money maxCapital, int maxLevels, decimal baseStepPercent, decimal stepScale, decimal volumeScale)
    {
        if (firstOrderVolume.Value <= 0) throw new ArgumentException("FirstOrderVolume must be positive.");
        if (maxCapital.Value <= 0) throw new ArgumentException("MaxCapital must be positive.");
        if (maxLevels < 0) throw new ArgumentException("MaxLevels cannot be negative.");
        if (baseStepPercent <= 0) throw new ArgumentException("BaseStepPercent must be positive.");
        if (stepScale <= 0) throw new ArgumentException("StepScale must be positive.");
        if (volumeScale <= 0) throw new ArgumentException("VolumeScale must be positive.");

        FirstOrderVolume = firstOrderVolume;
        MaxCapital = maxCapital;
        MaxLevels = maxLevels;
        BaseStepPercent = baseStepPercent;
        StepScale = stepScale;
        VolumeScale = volumeScale;
    }
}
