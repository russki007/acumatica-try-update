using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace russki007;

internal static class ReportWriter
{
	public static void Write(string reportPath, IEnumerable<SourceFile> formattedFiles, ILogger logger)
	{
		var reportFilePath = GetReportFilePath(reportPath);
		var reportFolderPath = Path.GetDirectoryName(reportFilePath);

		if (!string.IsNullOrEmpty(reportFolderPath) && !Directory.Exists(reportFolderPath))
			Directory.CreateDirectory(reportFolderPath);

		logger.LogInformation("Writing change report to: '{0}'", reportFilePath);

		var seralizerOptions = new JsonSerializerOptions { WriteIndented = true };
		File.WriteAllText(reportFilePath, JsonSerializer.Serialize(formattedFiles, seralizerOptions));
	}

	private static string GetReportFilePath(string reportPath)
	{
		var defaultReportName = $"dacs_{DateTime.Now:yyyymmdd}.json";
		return reportPath.EndsWith(".json") ? reportPath :
			reportPath == "." ? Path.Combine(Environment.CurrentDirectory, defaultReportName) :
			Path.Combine(reportPath, defaultReportName);
	}
}
