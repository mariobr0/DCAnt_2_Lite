using System;
using System.Threading;

namespace DCAnt2.Core.Domain;

/// <summary>
/// Реализует механизм Polling для регулярного опроса цены и защиты от Quote Storms
/// </summary>
public class QuotePoller : IDisposable
{
    private readonly Func<Price?> _priceProvider;
    private readonly Action<MarketQuoteMessage> _onQuote;
    private readonly TimeSpan _interval;
    private Timer? _timer;

    public QuotePoller(Func<Price?> priceProvider, Action<MarketQuoteMessage> onQuote, TimeSpan interval)
    {
        _priceProvider = priceProvider ?? throw new ArgumentNullException(nameof(priceProvider));
        _onQuote = onQuote ?? throw new ArgumentNullException(nameof(onQuote));
        _interval = interval;
    }

    public void Start()
    {
        // Не запускаем несколько раз
        _timer ??= new Timer(OnTick, null, TimeSpan.Zero, _interval);
    }

    public void Stop()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void OnTick(object? state)
    {
        try
        {
            var price = _priceProvider();
            if (price.HasValue)
            {
                _onQuote(new MarketQuoteMessage(price.Value));
            }
        }
        catch
        {
            // Игнорируем ошибки провайдера (например, нет соединения с биржей),
            // чтобы таймер не падал и продолжал опрашивать
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
