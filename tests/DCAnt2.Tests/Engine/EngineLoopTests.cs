using System;
using System.Reflection;
using DCAnt2.Core.Engine;
using Xunit;

namespace DCAnt2.Tests.Engine;

public sealed class EngineLoopTests
{
    private sealed record TestMessage(int Number) : EngineMessage;

    [Fact]
    public void Constructor_InitialState_IsCreated()
    {
        var loop = new EngineLoop(_ => { });
        Assert.Equal(EngineLoopState.Created, loop.State);
    }

    [Fact]
    public void Constructor_WithNullHandler_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new EngineLoop(null!));
    }

    [Fact]
    public void Start_ChangesStateToRunning()
    {
        var loop = new EngineLoop(_ => { });
        loop.Start();
        Assert.Equal(EngineLoopState.Running, loop.State);
    }

    [Fact]
    public void Start_WhenAlreadyRunning_ThrowsInvalidOperationException()
    {
        var loop = new EngineLoop(_ => { });
        loop.Start();

        Assert.Throws<InvalidOperationException>(() => loop.Start());
    }

    [Fact]
    public void Start_AfterStopped_ThrowsInvalidOperationException()
    {
        var loop = new EngineLoop(_ => { });
        
        // Взлом состояния для теста, пока нет полноценного StopAsync
        var stateField = typeof(EngineLoop).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
        stateField!.SetValue(loop, EngineLoopState.Stopped);

        Assert.Throws<InvalidOperationException>(() => loop.Start());
    }

    [Fact]
    public void TryEnqueue_WithNullMessage_ThrowsArgumentNullException()
    {
        var loop = new EngineLoop(_ => { });
        Assert.Throws<ArgumentNullException>(() => loop.TryEnqueue(null!));
    }

    [Fact]
    public void TryEnqueue_BeforeStart_ReturnsFalse()
    {
        var loop = new EngineLoop(_ => { });
        var result = loop.TryEnqueue(new TestMessage(1));
        Assert.False(result);
    }

    [Fact]
    public void TryEnqueue_WhenRunning_ReturnsTrue()
    {
        var loop = new EngineLoop(_ => { });
        loop.Start();
        var result = loop.TryEnqueue(new TestMessage(1));
        Assert.True(result);
    }

    [Fact]
    public async System.Threading.Tasks.Task Messages_AreProcessedInEnqueueOrder()
    {
        var processed = new System.Collections.Generic.List<int>();
        var tcs = new System.Threading.Tasks.TaskCompletionSource();
        
        var loop = new EngineLoop(msg => 
        {
            if (msg is TestMessage tm)
            {
                processed.Add(tm.Number);
                if (processed.Count == 3)
                {
                    tcs.SetResult();
                }
            }
        });
        
        loop.Start();
        loop.TryEnqueue(new TestMessage(1));
        loop.TryEnqueue(new TestMessage(2));
        loop.TryEnqueue(new TestMessage(3));
        
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        
        Assert.Equal(new[] { 1, 2, 3 }, processed);
    }

    [Fact]
    public async System.Threading.Tasks.Task Messages_AreNeverProcessedConcurrently()
    {
        int activeHandlers = 0;
        int maxActiveHandlers = 0;
        var processedCount = 0;
        var tcs = new System.Threading.Tasks.TaskCompletionSource();
        
        var loop = new EngineLoop(msg => 
        {
            var current = System.Threading.Interlocked.Increment(ref activeHandlers);
            
            lock (tcs) 
            {
                if (current > maxActiveHandlers) maxActiveHandlers = current;
            }
            
            // Имитация короткой задержки без Thread.Sleep
            System.Threading.SpinWait.SpinUntil(() => false, 10);
            
            System.Threading.Interlocked.Decrement(ref activeHandlers);
            
            var total = System.Threading.Interlocked.Increment(ref processedCount);
            if (total == 5)
            {
                tcs.SetResult();
            }
        });
        
        loop.Start();
        for (int i = 0; i < 5; i++)
        {
            loop.TryEnqueue(new TestMessage(i));
        }
        
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        
        Assert.Equal(1, maxActiveHandlers);
    }

    [Fact]
    public async System.Threading.Tasks.Task StopAsync_ProcessesAcceptedMessagesBeforeStopping()
    {
        var processed = new System.Collections.Generic.List<int>();
        var tcs = new System.Threading.Tasks.TaskCompletionSource();
        
        var loop = new EngineLoop(msg => 
        {
            if (msg is TestMessage tm)
            {
                processed.Add(tm.Number);
                if (processed.Count == 3)
                {
                    tcs.SetResult();
                }
            }
        });
        
        loop.Start();
        loop.TryEnqueue(new TestMessage(1));
        loop.TryEnqueue(new TestMessage(2));
        loop.TryEnqueue(new TestMessage(3));
        
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        
        await loop.StopAsync();
        
        Assert.Equal(EngineLoopState.Stopped, loop.State);
        Assert.Equal(new[] { 1, 2, 3 }, processed);
    }

    [Fact]
    public async System.Threading.Tasks.Task StopAsync_CanBeCalledTwice()
    {
        var loop = new EngineLoop(_ => { });
        loop.Start();
        
        await loop.StopAsync();
        await loop.StopAsync();
        
        Assert.Equal(EngineLoopState.Stopped, loop.State);
    }

    [Fact]
    public async System.Threading.Tasks.Task TryEnqueue_AfterStop_ReturnsFalse()
    {
        var loop = new EngineLoop(_ => { });
        loop.Start();
        await loop.StopAsync();
        
        var result = loop.TryEnqueue(new TestMessage(1));
        Assert.False(result);
    }

    [Fact]
    public async System.Threading.Tasks.Task StopAsync_BeforeStart_ChangesStateToStopped()
    {
        var loop = new EngineLoop(_ => { });
        await loop.StopAsync();
        
        Assert.Equal(EngineLoopState.Stopped, loop.State);
        Assert.False(loop.TryEnqueue(new TestMessage(1)));
        Assert.Throws<InvalidOperationException>(() => loop.Start());
    }

    [Fact]
    public async System.Threading.Tasks.Task HandlerException_ChangesStateToFaulted()
    {
        var panicTcs = new System.Threading.Tasks.TaskCompletionSource<Exception>();
        var loop = new EngineLoop(
            _ => throw new InvalidOperationException("Handler failed"),
            ex => panicTcs.SetResult(ex)
        );
        
        loop.Start();
        loop.TryEnqueue(new TestMessage(1));
        
        var ex = await panicTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        
        Assert.IsType<InvalidOperationException>(ex);
        Assert.Equal("Handler failed", ex.Message);
        Assert.Equal(EngineLoopState.Faulted, loop.State);
        Assert.False(loop.TryEnqueue(new TestMessage(2)));
    }

    [Fact]
    public async System.Threading.Tasks.Task HandlerException_StateRemainsFaultedAfterStopAsync()
    {
        var panicTcs = new System.Threading.Tasks.TaskCompletionSource<Exception>();
        var loop = new EngineLoop(
            _ => throw new InvalidOperationException("Handler failed"),
            ex => panicTcs.SetResult(ex)
        );
        
        loop.Start();
        loop.TryEnqueue(new TestMessage(1));
        
        await panicTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        
        await loop.StopAsync();
        
        Assert.Equal(EngineLoopState.Faulted, loop.State);
    }

    [Fact]
    public async System.Threading.Tasks.Task PanicHandlerException_DoesNotPreventLoopCompletion()
    {
        var panicTcs = new System.Threading.Tasks.TaskCompletionSource();
        var loop = new EngineLoop(
            _ => throw new InvalidOperationException("Handler failed"),
            _ => 
            {
                panicTcs.SetResult();
                throw new InvalidOperationException("Panic handler failed");
            }
        );
        
        loop.Start();
        loop.TryEnqueue(new TestMessage(1));
        
        await panicTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        
        await loop.StopAsync();
        
        Assert.Equal(EngineLoopState.Faulted, loop.State);
    }

    [Fact]
    public async System.Threading.Tasks.Task DisposeAsync_StopsRunningLoop()
    {
        var loop = new EngineLoop(_ => { });
        loop.Start();
        
        await loop.DisposeAsync();
        
        Assert.Equal(EngineLoopState.Stopped, loop.State);
    }
}
