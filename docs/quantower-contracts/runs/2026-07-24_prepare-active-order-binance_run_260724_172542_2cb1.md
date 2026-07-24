# Prepare Active Order (Binance)

**Scenario:** PrepareActiveOrder
**RunId:** run_260724_172542_2cb1
**Environment:** Binance USDT-M Futures
**Account type:** Live
**EvidencePseudonymized:** True
**PseudonymizedFields:** AccountName,ConnectionId
*Raw evidence was pseudonymized before repository storage.*

## Results

- **TargetMarker:** qtct_240726_750fcb40
- **OrderId:** 1854362432
- **OrderPrice:** 0.03999
- **OrderTotalQuantity:** 300
- **Status:** Opened

В отличие от Trading Simulator, на Binance ордер уже находился в активной коллекции в строке `AfterApiCall`:
В этом запуске к моменту возврата `PlaceOrder` ордер уже был доступен в наблюдаемом snapshot. Нельзя переносить это поведение на другие сценарии или считать его гарантией для всех Binance-запросов.
