namespace DCAnt2.Core.Domain;

public record TradeCycleSnapshot(
    int GeneratedGridLevels,
    int FilledGridLevels,
    int ActiveGridOrdersCount,
    decimal EntryPrice,
    decimal BaseQuantity,
    decimal? LastTpPrice,
    bool TpUpdateRequired,
    decimal? SlPrice
);
