using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace russki007;

internal class SimpleConsoleLogger : ILogger
{
	private readonly object _gate = new();

	private readonly IConsole _console;
	private readonly LogLevel _minimalLogLevel;
	private readonly LogLevel _minimalErrorLevel;

	public SimpleConsoleLogger(IConsole console, LogLevel minimalLogLevel, LogLevel minimalErrorLevel)
	{ 
		_console = console;
		_minimalLogLevel = minimalLogLevel;
		_minimalErrorLevel = minimalErrorLevel;
	}

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		if (!IsEnabled(logLevel))
		{
			return;
		}

		lock (_gate)
		{
			var message = formatter(state, exception);
			var logToErrorStream = logLevel >= _minimalErrorLevel;

			LogToConsole(_console, message, logToErrorStream);
		}
	}

	public bool IsEnabled(LogLevel logLevel)
	{
		return (int)logLevel >= (int)_minimalLogLevel;
	}

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull
	{
		return NullScope.Instance;
	}

	private static void LogToConsole(IConsole console, string message, bool logToErrorStream)
	{
		if (logToErrorStream)
		{
			console.Error.Write($"{message}{Environment.NewLine}");
		}
		else
		{
			console.Out.Write($"{message}{Environment.NewLine}");
		}
	}
}
