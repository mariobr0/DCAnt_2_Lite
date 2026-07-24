# Scenario A Analysis (Strategy Restart with Active Order)

**Scenario:** ObserveRecovery
**Recovery scenario:** StrategyRestartWithActiveOrder
**RunId:** run_260724_154349_d031
**Result:** Passed
**EvidenceQuality:** High

## Expected Parameters
- TargetMarker: qtct_240726_d0a29466
- ExpectedOrderId: d502dd3a-32d7-4bb6-b65e-0f477d7be7b7
- ExpectedOrderPrice: 0.076
- ExpectedOrderTotalQuantity: 10 (per preparation evidence, though missed in input parameters)

## Validation

1. **Active Order Visibility:** Confirmed. The `StartSnapshot` correctly found the active order directly from `Core.Orders` with `OrderPresentInActiveCollection=True` and `Classification=CurrentSnapshot`.
2. **OrderId Correlation:** Confirmed. `ExpectedOrderIdMatches=True`.
3. **Comment Correlation:** Confirmed. Target marker remained intact.
4. **Price Correlation:** Confirmed. `ExpectedOrderPriceMatches=True`.
5. **Quantity Correlation:** Visually confirmed as 10.
6. **Status:** Confirmed. Status remained `Opened`.
7. **Position:** Confirmed. Position remained `None`, quantity `0`.
8. **Repeated Callbacks:** No `OrderEvent` or `OrderHistoryEvent` callbacks were observed between `StartSnapshot` and `OnStop` during this run.
