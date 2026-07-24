using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DCAnt2.Core.Engine;

/// <summary>
/// Изолированный, последовательный цикл обработки сообщений.
/// </summary>
public sealed class EngineLoop : IAsyncDisposable
{
    private const int ChannelCapacity = 1024;
    private readonly object _stateLock = new();
    private readonly Channel<EngineMessage> _channel;
    private readonly Action<EngineMessage> _messageHandler;
    private readonly Action<Exception>? _panicHandler;

    private EngineLoopState _state = EngineLoopState.Created;
    private Task? _loopTask;

    public EngineLoop(Action<EngineMessage> messageHandler, Action<Exception>? panicHandler = null)
    {
        ArgumentNullException.ThrowIfNull(messageHandler);

        _messageHandler = messageHandler;
        _panicHandler = panicHandler;

        _channel = Channel.CreateBounded<EngineMessage>(
            new BoundedChannelOptions(ChannelCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });
    }

    public EngineLoopState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }

    public bool TryEnqueue(EngineMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (_stateLock)
        {
            if (_state != EngineLoopState.Running)
            {
                return false;
            }

            return _channel.Writer.TryWrite(message);
        }
    }

    public void Start()
    {
        lock (_stateLock)
        {
            if (_state != EngineLoopState.Created)
            {
                throw new InvalidOperationException($"Cannot start EngineLoop from state {_state}");
            }

            _state = EngineLoopState.Running;
            _loopTask = Task.Run(ReadAllAsync);
        }
    }

    public async Task StopAsync()
    {
        Task? loopTaskToAwait = null;

        lock (_stateLock)
        {
            switch (_state)
            {
                case EngineLoopState.Created:
                    _channel.Writer.TryComplete();
                    _state = EngineLoopState.Stopped;
                    return;
                case EngineLoopState.Running:
                    _state = EngineLoopState.Stopping;
                    _channel.Writer.TryComplete();
                    loopTaskToAwait = _loopTask;
                    break;
                case EngineLoopState.Stopping:
                case EngineLoopState.Faulted:
                    loopTaskToAwait = _loopTask;
                    break;
                case EngineLoopState.Stopped:
                    return;
            }
        }

        if (loopTaskToAwait is not null)
        {
            await loopTaskToAwait.ConfigureAwait(false);
        }
    }

    private async Task ReadAllAsync()
    {
        try
        {
            await foreach (var message in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                _messageHandler(message);
            }

            lock (_stateLock)
            {
                if (_state == EngineLoopState.Stopping)
                {
                    _state = EngineLoopState.Stopped;
                }
            }
        }
        catch (Exception ex)
        {
            lock (_stateLock)
            {
                _state = EngineLoopState.Faulted;
                _channel.Writer.TryComplete();
            }

            try
            {
                _panicHandler?.Invoke(ex);
            }
            catch
            {
                // Ignore panic handler exception to avoid hanging the task completion
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }
}
