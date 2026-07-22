using System;
using System.Linq;
using System.Threading;
using TradingPlatform.BusinessLayer;

namespace DCAnt2.Plugin
{
    public class DiagnosticContractTestStrategy : Strategy
    {
        [InputParameter("Test Symbol", 0)]
        public Symbol TestSymbol = default!;

        [InputParameter("Test Account", 1)]
        public Account TestAccount = default!;

        public DiagnosticContractTestStrategy()
        {
            Name = "DCAnt2 Diagnostic Tests";
            Description = "Tests Quantower API behavior (Idempotency, Comments, etc.)";
        }

        protected override void OnRun()
        {
            if (TestSymbol == null || TestAccount == null)
            {
                Log("DCAnt Diagnostic: Symbol or Account is not selected.", StrategyLoggingLevel.Error);
                return;
            }

            Log("=== Starting Modify Order Diagnostics ===", StrategyLoggingLevel.Info);
            
            string testComment = "ModTest_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Log($"Test Comment: {testComment}", StrategyLoggingLevel.Info);

            var param = new PlaceOrderRequestParameters()
            {
                Account = TestAccount,
                Symbol = TestSymbol,
                Side = Side.Buy,
                TimeInForce = TimeInForce.GTC,
                OrderTypeId = OrderType.Limit,
                Price = TestSymbol.Bid - (TestSymbol.TickSize * 200), // Далеко от рынка
                Quantity = TestSymbol.MinLot,
                Comment = testComment
            };

            // 1. Размещаем ордер
            Log("1. Sending Order...", StrategyLoggingLevel.Info);
            var result = Core.PlaceOrder(param);
            
            if (result.Status != TradingOperationResultStatus.Success)
            {
                Log($"Order Failed: {result.Message}", StrategyLoggingLevel.Error);
                return;
            }
            
            Log($"Order Success. Local ID: {result.OrderId}", StrategyLoggingLevel.Info);
            
            // Ждем 3 секунды, чтобы ордер появился на бирже и в Quantower
            Log("Waiting 3 seconds for sync...", StrategyLoggingLevel.Info);
            Thread.Sleep(3000);

            // 2. Ищем ордер
            var foundOrder = Core.Orders.FirstOrDefault(x => x.Comment == testComment && x.Status == OrderStatus.Opened);
            
            if (foundOrder == null)
            {
                Log("Order not found in Core.Orders! Cannot test Modify.", StrategyLoggingLevel.Error);
                return;
            }
            
            Log($"Found Order: Id={foundOrder.Id}, Price={foundOrder.Price}, Comment={foundOrder.Comment}", StrategyLoggingLevel.Info);

            // 3. Модифицируем ордер (изменяем цену)
            double newPrice = foundOrder.Price + TestSymbol.TickSize * 10;
            Log($"2. Modifying Order Price to {newPrice}...", StrategyLoggingLevel.Info);
            
            var modifyResult = Core.ModifyOrder(foundOrder, price: newPrice);
            
            if (modifyResult.Status != TradingOperationResultStatus.Success)
            {
                Log($"Modify Failed: {modifyResult.Message}", StrategyLoggingLevel.Error);
                return;
            }
            
            Log("Modify Command Sent. Waiting 3 seconds for sync...", StrategyLoggingLevel.Info);
            Thread.Sleep(3000);

            // 4. Проверяем, сохранился ли Comment
            var modifiedOrder = Core.Orders.FirstOrDefault(x => x.Id == foundOrder.Id || x.Comment == testComment);
            
            if (modifiedOrder != null)
            {
                Log($"Result After Modify: Id={modifiedOrder.Id}, Price={modifiedOrder.Price}, Status={modifiedOrder.Status}, Comment={modifiedOrder.Comment}", StrategyLoggingLevel.Info);
                
                if (modifiedOrder.Comment == testComment)
                    Log("SUCCESS: Comment SURVIVED the modification!", StrategyLoggingLevel.Info);
                else
                    Log("WARNING: Comment was LOST or CHANGED after modification!", StrategyLoggingLevel.Error);
            }
            else
            {
                Log("WARNING: Order disappeared from Core.Orders after modify! (Maybe Cancel/Replace created a new ID?)", StrategyLoggingLevel.Error);
                
                // Попытаемся найти любой открытый ордер с нашим комментом
                var anyOrderWithComment = Core.Orders.FirstOrDefault(x => x.Comment == testComment);
                if (anyOrderWithComment != null)
                {
                    Log($"Found a NEW order with our Comment: Id={anyOrderWithComment.Id}, Price={anyOrderWithComment.Price}", StrategyLoggingLevel.Info);
                }
            }

            Log("=== Diagnostics Finished ===", StrategyLoggingLevel.Info);
            Log("Please cancel the open orders manually.", StrategyLoggingLevel.Info);
        }
    }
}
