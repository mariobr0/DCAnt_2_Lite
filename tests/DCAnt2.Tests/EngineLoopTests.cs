using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DCAnt2.Core.Domain;
using Xunit;

namespace DCAnt2.Tests;

public class EngineLoopTests
{
    [Fact]
    public async Task EngineLoop_ProcessesMessagesSequentially_AndAllowsSoftShutdown()
    {
        // Arrange
        var processed = new List<string>();
        var tcs = new TaskCompletionSource();
        
        using var loop = new EngineLoop(
            msg =>
            {
                if (msg is MarketQuoteMessage mq)
                {
                    processed.Add($"Quote:{mq.Price.Value}");
                }
                else if (msg is ExecutionMessage)
                {
                    processed.Add("Execution");
                }
            },
            ex => { }
        );

        loop.Start();

        // Act
        loop.Enqueue(new MarketQuoteMessage(new Price(100m)));
        loop.Enqueue(new ExecutionMessage(null!)); // Just for testing sequence
        loop.Enqueue(new MarketQuoteMessage(new Price(101m)));

        await loop.StopAsync(); // Enqueues ShutdownMessage and waits

        // Assert
        Assert.Equal(3, processed.Count);
        Assert.Equal("Quote:100", processed[0]);
        Assert.Equal("Execution", processed[1]);
        Assert.Equal("Quote:101", processed[2]);
    }

    [Fact]
    public async Task EngineLoop_OnUnhandledException_TriggersPanicAndStops()
    {
        // Arrange
        Exception? caughtException = null;
        var panicEvent = new ManualResetEventSlim(false);

        using var loop = new EngineLoop(
            msg =>
            {
                if (msg is MarketQuoteMessage)
                {
                    throw new InvalidOperationException("Simulated bug");
                }
            },
            ex =>
            {
                caughtException = ex;
                panicEvent.Set();
            }
        );

        loop.Start();

        // Act
        loop.Enqueue(new MarketQuoteMessage(new Price(100m)));

        // Assert
        bool panicked = panicEvent.Wait(TimeSpan.FromSeconds(2));
        Assert.True(panicked, "Panic should have been triggered.");
        Assert.NotNull(caughtException);
        Assert.IsType<InvalidOperationException>(caughtException);
        
        await loop.StopAsync();
    }

    [Fact]
    public async Task QuotePoller_GeneratesMessagesAtIntervals()
    {
        // Arrange
        var quotes = new ConcurrentBag<MarketQuoteMessage>();
        decimal currentPrice = 100m;
        
        using var poller = new QuotePoller(
            () =>
            {
                currentPrice += 1m;
                return new Price(currentPrice);
            },
            msg => quotes.Add(msg),
            TimeSpan.FromMilliseconds(50)
        );

        // Act
        poller.Start();
        await Task.Delay(200); // Should fire roughly 3-4 times
        poller.Stop();

        // Assert
        Assert.True(quotes.Count >= 2, $"Should have polled at least twice, got {quotes.Count}");
    }
}
