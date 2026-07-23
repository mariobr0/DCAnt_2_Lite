using System.Collections.Generic;

namespace DCAnt2.Core.Domain;

public interface ITradingStateStore
{
    void SaveStateAndIntents(TradeCycle cycle, IEnumerable<TradeIntent> intents, OrderExecuted? execution = null);
    TradeCycle? LoadActiveCycle(InstrumentRules rules, DcaSettings settings);
    bool IsExecutionProcessed(string executionId);
}
