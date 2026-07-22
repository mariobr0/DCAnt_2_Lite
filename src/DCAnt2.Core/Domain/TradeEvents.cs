namespace DCAnt2.Core.Domain;

public abstract record TradeEvent;

public record OrderExecuted(
    InternalOrderId OrderId, 
    Price ExecutedPrice, 
    Quantity ExecutedQuantity) : TradeEvent;

public record OrderRejected(
    InternalOrderId OrderId, 
    string Reason) : TradeEvent;
