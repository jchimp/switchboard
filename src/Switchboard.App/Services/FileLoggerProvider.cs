using System.IO;
using Microsoft.Extensions.Logging;
using Switchboard.Core.Logging;

namespace Switchboard.App.Services;

/// <summary>
/// Minimal daily-file logger. Deliberately hand-rolled: Serilog/NLog would be the repo's
/// first heavyweight dependencies for ~a hundred lines of need. Logging must never crash
/// the app, so every IO failure is swallowed.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly object _sync = new();
    private StreamWriter? _writer;
    private DateOnly _writerDay;

    public FileLoggerProvider(string directory)
    {
        _directory = directory;
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
        lock (_sync)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void Write(string line)
    {
        lock (_sync)
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                if (_writer is null || today != _writerDay)
                {
                    _writer?.Dispose();
                    _writer = Open(today);
                    _writerDay = today;
                }

                _writer?.WriteLine(line);
            }
            catch (Exception)
            {
                // A full disk or locked file must not take the app down; drop the line.
                _writer = null;
            }
        }
    }

    private StreamWriter? Open(DateOnly today)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            Prune(today);
            var path = Path.Combine(_directory, LogFile.FileNameFor(today));
            return new StreamWriter(path, append: true) { AutoFlush = true };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void Prune(DateOnly today)
    {
        var names = Directory.EnumerateFiles(_directory).Select(Path.GetFileName).OfType<string>();
        foreach (var name in LogFile.SelectPrunable(names, today))
        {
            try
            {
                File.Delete(Path.Combine(_directory, name));
            }
            catch (IOException)
            {
                // Still open elsewhere; it'll go next time.
            }
        }
    }

    private sealed class FileLogger : ILogger
    {
        private static readonly string[] LevelTags = ["TRC", "DBG", "INF", "WRN", "ERR", "CRT", "---"];

        private readonly FileLoggerProvider _provider;
        private readonly string _category;

        public FileLogger(FileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _provider.Write(LogFile.FormatLine(
                DateTimeOffset.Now, LevelTags[(int)logLevel], _category,
                formatter(state, exception), exception?.ToString()));
        }
    }
}
