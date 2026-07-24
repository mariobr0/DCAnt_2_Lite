using System;

namespace DCAnt2.Core.Domain;

public record InstrumentRules
{
    public string QuoteCurrency { get; }
    public decimal TickSize { get; }
    public decimal QuantityStep { get; }
    public decimal MinNotional { get; }

    public InstrumentRules(string quoteCurrency, decimal tickSize, decimal quantityStep, decimal minNotional)
    {
        if (string.IsNullOrWhiteSpace(quoteCurrency)) throw new ArgumentException("Quote currency must be provided.");
        if (tickSize <= 0) throw new ArgumentOutOfRangeException(nameof(tickSize), "Tick size must be greater than zero.");
        if (quantityStep <= 0) throw new ArgumentOutOfRangeException(nameof(quantityStep), "Quantity step must be greater than zero.");
        if (minNotional < 0) throw new ArgumentOutOfRangeException(nameof(minNotional), "Min notional cannot be negative.");

        QuoteCurrency = quoteCurrency;
        TickSize = tickSize;
        QuantityStep = quantityStep;
        MinNotional = minNotional;
    }

    public Price RoundPriceToNearest(Price price)
    {
        if (price.Value == 0) return price;
        decimal roundedValue = Math.Round(price.Value / TickSize, MidpointRounding.AwayFromZero) * TickSize;
        return new Price(roundedValue);
    }

    public Quantity RoundQuantityDown(Quantity qty)
    {
        if (qty.Value == 0) return qty;
        decimal roundedValue = Math.Floor(qty.Value / QuantityStep) * QuantityStep;
        return new Quantity(roundedValue);
    }
}
