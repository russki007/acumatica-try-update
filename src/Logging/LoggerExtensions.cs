using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace russki007;

internal static class LoggerExtensions
{
	public static ILoggerFactory AddSimpleConsole(this ILoggerFactory factory, IConsole console, LogLevel minimalLogLevel, LogLevel minimalErrorLevel)
	{
		factory.AddProvider(new SimpleConsoleLoggerProvider(console, minimalLogLevel, minimalErrorLevel));
		return factory;
	}

	public static ILogger<Program> SetupLogging(this IConsole console, LogLevel minimalLogLevel, LogLevel minimalErrorLevel)
	{
		var loggerFactory = new LoggerFactory()
			.AddSimpleConsole(console, minimalLogLevel, minimalErrorLevel);
		var logger = loggerFactory.CreateLogger<Program>();
		return logger;
	}
}