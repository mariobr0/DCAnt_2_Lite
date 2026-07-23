namespace DCAnt2.Core.Domain;

public record DcaSettings(
    decimal TpPercent,
    decimal StepPercent,
    decimal VolumeScale,
    int MaxGridLevels,
    int ActiveGridWindow,
    decimal LastLevelStopPercent
);
