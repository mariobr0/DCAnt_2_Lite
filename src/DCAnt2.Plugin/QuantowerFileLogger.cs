using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Channels;
using System.Threading.Tasks;
using DCAnt2.Core;
using TradingPlatform.BusinessLayer;

namespace DCAnt2.Plugin;

public class QuantowerFileLogger : IBotLogger, IDisposable
{
    private readonly Strategy _strategy;
    private readonly Action<string, StrategyLoggingLevel> _uiLogger;
    private readonly string _filePath;
    private readonly Channel<string> _channel;
    private readonly Task _workerTask;
    private bool _disposed;

    public QuantowerFileLogger(Strategy strategy, string strategyName, Action<string, StrategyLoggingLevel> uiLogger)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _uiLogger = uiLogger ?? throw new ArgumentNullException(nameof(uiLogger));
        
        var logsDir = @"C:\Users\dzam\Desktop\Code\QuantowerCode\DCAnt_2_Lite\logs";
        Directory.CreateDirectory(logsDir);
        
        var timestamp = DateTime.Now.ToString("yy-MM-dd_HH-mm-ss");
        var guid = Guid.NewGuid().ToString("N").Substring(0, 8);
        
        _filePath = Path.Combine(logsDir, $"DCAnt_2_{timestamp}_{guid}.txt");

        _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        DumpConfiguration();

        _workerTask = Task.Run(ProcessQueueAsync);
    }

    private void DumpConfiguration()
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== BOT CONFIGURATION ===");
            sb.AppendLine($"Strategy: {_strategy.GetType().Name}");
            sb.AppendLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            
            var properties = _strategy.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                var inputAttr = prop.GetCustomAttribute<InputParameterAttribute>();
                if (inputAttr != null)
                {
                    sb.AppendLine($"- {inputAttr.Name}: {FormatValue(prop.GetValue(_strategy))}");
                }
            }
            
            var fields = _strategy.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var inputAttr = field.GetCustomAttribute<InputParameterAttribute>();
                if (inputAttr != null)
                {
                    sb.AppendLine($"- {inputAttr.Name}: {FormatValue(field.GetValue(_strategy))}");
                }
            }
            
            sb.AppendLine("=========================");
            sb.AppendLine();
            
            File.AppendAllText(_filePath, sb.ToString());
        }
        catch (Exception ex)
        {
            _uiLogger($"Failed to dump configuration to log: {ex.Message}", StrategyLoggingLevel.Error);
        }
    }
    
    private string FormatValue(object? value)
    {
        if (value == null) return "null";
        
        if (value is Symbol s) return s.Name;
        if (value is Account a) return $"{a.Name} ({a.Id})";
        
        return value.ToString() ?? "null";
    }

    public void Info(string message)
    {
        Log("INFO", message, StrategyLoggingLevel.Info);
    }

    public void Error(string message)
    {
        Log("ERROR", message, StrategyLoggingLevel.Error);
    }

    private void Log(string level, string message, StrategyLoggingLevel qtLevel)
    {
        if (_disposed) return;
        
        var time = DateTime.Now.ToString("yy-MM-dd HH:mm:ss.fff");
        var line = $"[{time}] [{level}] {message}";
        
        // Write to QT immediately
        _uiLogger(message, qtLevel);
        
        // Push to channel for file writing
        _channel.Writer.TryWrite(line);
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var line in _channel.Reader.ReadAllAsync())
            {
                File.AppendAllText(_filePath, line + Environment.NewLine);
            }
        }
        catch (Exception)
        {
            // Ignore background write errors
        }
    }

    public void Stop()
    {
        if (_disposed) return;
        _disposed = true;
        
        _channel.Writer.Complete();
        _workerTask.Wait(TimeSpan.FromSeconds(2)); // wait for flush
        
        try
        {
            File.AppendAllText(_filePath, $"[{DateTime.Now:yy-MM-dd HH:mm:ss.fff}] [INFO] === BOT STOPPED ===" + Environment.NewLine);
        }
        catch { }
    }

    public void Dispose()
    {
        Stop();
    }
}
