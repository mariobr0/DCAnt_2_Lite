using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DCAnt2.Core.Domain;

/// <summary>
/// Однопоточный цикл обработки сообщений (Actor Model) для защиты состояния от Race Conditions
/// </summary>
public class EngineLoop : IDisposable
{
    private readonly Action<EngineMessage> _messageHandler;
    private readonly Action<Exception> _onPanic;
    private readonly Channel<EngineMessage> _channel;
    
    private CancellationTokenSource? _shutdownCts;
    private Task? _loopTask;
    private int _isDisposed;

    public EngineLoop(Action<EngineMessage> messageHandler, Action<Exception> onPanic)
    {
        _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        _onPanic = onPanic ?? throw new ArgumentNullException(nameof(onPanic));
        
        // Unbounded channel ensures we never block the caller (e.g. Quantower API callback thread)
        _channel = Channel.CreateUnbounded<EngineMessage>(new UnboundedChannelOptions
        {
            SingleReader = true, // Performance optimization: only EngineLoop reads
            SingleWriter = false 
        });
    }

    /// <summary>
    /// Поставить сообщение в очередь (потокобезопасно)
    /// </summary>
    public void Enqueue(EngineMessage message)
    {
        if (Interlocked.CompareExchange(ref _isDisposed, 0, 0) == 1) return;
        _channel.Writer.TryWrite(message);
    }

    /// <summary>
    /// Запустить фоновый поток обработки
    /// </summary>
    public void Start()
    {
        if (_loopTask != null) return;
        
        _shutdownCts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunLoopAsync(_shutdownCts.Token));
    }

    /// <summary>
    /// Мягкая остановка цикла через ShutdownMessage
    /// </summary>
    public async Task StopAsync()
    {
        Enqueue(new ShutdownMessage());
        
        if (_loopTask != null)
        {
            await _loopTask;
            _loopTask = null;
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var message in _channel.Reader.ReadAllAsync(ct))
            {
                if (message is ShutdownMessage)
                {
                    // Мягкая остановка
                    break;
                }

                _messageHandler(message);
            }
        }
        catch (OperationCanceledException)
        {
            // Ожидаемо при жесткой отмене (Dispose)
        }
        catch (Exception ex)
        {
            // Системный/Программный сбой внутри цикла -> Panic
            _onPanic(ex);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1) return;
        
        _shutdownCts?.Cancel();
        _shutdownCts?.Dispose();
    }
}
