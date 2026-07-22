namespace DCAnt2.Core.Domain;

public abstract record TradeIntent;

public enum OrderPurpose 
{ 
    FirstOrder, 
    DcaOrder, 
    TakeProfit, 
    StopLoss 
}

public record PlaceOrderIntent(
    InternalOrderId OrderId, 
    OrderPurpose Purpose, 
    OrderSide Side, 
    Price Price, 
    Quantity Quantity) : TradeIntent;

public record CancelOrderIntent(
    InternalOrderId OrderId) : TradeIntent;
