namespace DCAnt2.Core.Domain;

/// <summary>
/// Базовый класс для всех сообщений, поступающих в EngineLoop
/// </summary>
public abstract record EngineMessage;

/// <summary>
/// Уведомление об исполнении ордера от биржи
/// </summary>
public record ExecutionMessage(OrderExecuted Execution) : EngineMessage;

/// <summary>
/// Уведомление об отклонении ордера биржей
/// </summary>
public record RejectionMessage(OrderRejected Rejection) : EngineMessage;

/// <summary>
/// Тик времени
/// </summary>
public record TickMessage(DateTime Timestamp) : EngineMessage;

/// <summary>
/// Новая рыночная цена
/// </summary>
public record MarketQuoteMessage(Price Price) : EngineMessage;

/// <summary>
/// Команда на остановку цикла
/// </summary>
public record ShutdownMessage() : EngineMessage;

/// <summary>
/// Команда на старт нового цикла в непрерывном режиме
/// </summary>
public record StartNewCycleMessage() : EngineMessage;
