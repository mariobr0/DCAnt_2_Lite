namespace DCAnt2.Core.Engine;

/// <summary>
/// Состояния жизненного цикла <see cref="EngineLoop"/>.
/// </summary>
public enum EngineLoopState
{
    /// <summary>
    /// Объект создан, но обработка еще не запущена.
    /// </summary>
    Created,

    /// <summary>
    /// Движок принимает и обрабатывает сообщения.
    /// </summary>
    Running,

    /// <summary>
    /// Запрошена остановка. Новые сообщения отклоняются, идет обработка оставшихся в очереди.
    /// </summary>
    Stopping,

    /// <summary>
    /// Очередь исчерпана, обработка штатно завершена.
    /// </summary>
    Stopped,

    /// <summary>
    /// Аварийная остановка из-за исключения.
    /// </summary>
    Faulted
}
