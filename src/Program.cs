using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using static russki007.Utils;

namespace russki007;

public class Program
{
	public static async Task<int> Main(string[] args)
	{
		var slnOrProjectArgument = new Argument<string> { Arity = ArgumentArity.ZeroOrOne, Description = "The project or solution file to operate on. If a file is not specified, the command will search the current directory for one." };
		var reportPathOption = new Option<string>(["--report", "-r"], getDefaultValue: () => EnsureTrailingSlash(Directory.GetCurrentDirectory())) { Arity = ArgumentArity.ZeroOrOne, Description = "Accepts a file path which if provided will produce a json report in the given directory." };
		var applyChanges = new Option<bool>("--apply-changes", description: "No code changes will be performed unless this option is set; instead, only a report will be generated.", getDefaultValue: () => false);

		var rootCommand = new RootCommand(AppDomain.CurrentDomain.FriendlyName) { slnOrProjectArgument, reportPathOption, applyChanges };

		rootCommand.SetHandler(async (slnOrProjectFilePath, reportFilePath, applyChanges) =>
		{
			var logger = new SystemConsole().SetupLogging(minimalLogLevel: LogLevel.Debug, minimalErrorLevel: LogLevel.Trace);
			await FixAsync(slnOrProjectFilePath, reportFilePath, logger, applyChanges, CancellationToken.None);

		}, slnOrProjectArgument, reportPathOption, applyChanges);

		return await rootCommand.InvokeAsync(args);
	}

	static async Task<int> FixAsync(string solutionOrProjectFilePath, string reportFilePath, ILogger<Program> logger, bool applyChanges, CancellationToken cancellationToken = default)
	{
		logger.LogDebug("Analsysing workspace {0}", solutionOrProjectFilePath);

		if (!TryLoadMSBuild(out var msBuildPath))
		{
			logger.LogError("Unable to locate MSBuild. Ensure the .NET SDK was installed with the official installer.");
			return 1;
		}

		logger.LogDebug("Using MSBuild.exe located in '{0}'", msBuildPath);


		var workspace = await LoadWorkspaceAsync(solutionOrProjectFilePath, false, logger, cancellationToken);
		var solution = workspace?.CurrentSolution;

		if (solution is null)
		{
			logger.LogError("Unable to load solution or project file.");
			return 2;
		}


		var projects = solution.Projects
				.Where(project => IsPlatformReferenced(project, "PX.Data"));


		List<SourceFile> filesWithChagnes = new();


		foreach (var project in projects)
		{
			
			var metadataReferences = projects
											.SelectMany(proj => proj.MetadataReferences)
											.Distinct();


			foreach (var document in project.Documents)
			{
				SourceText text;
				try
				{
					using (var stream = File.OpenRead(document.FilePath))
					{
						text = SourceText.From(stream);
					}
				}
				catch (DirectoryNotFoundException)
				{
					logger.LogError($"Directory not found: {document.FilePath}");
					continue;
				}
				catch (FileNotFoundException)
				{
					logger.LogError($"File not found: {document.FilePath}");
					continue;
				}



				var fileChanges = new List<FileChange>();

				SyntaxTree initialTree = CSharpSyntaxTree.ParseText(text);
				var compilation = CSharpCompilation.Create(
					"test", syntaxTrees: [initialTree], references: metadataReferences);


				var rewriter = new BaseTypeRewriter(compilation.GetSemanticModel(initialTree), applyChanges, fileChanges.Add, logger);

				var treeWithMigratedDac = rewriter.Visit(initialTree.GetRoot()).SyntaxTree;

				if (fileChanges.Count != 0)
				{

					filesWithChagnes.Add(new SourceFile(document, fileChanges));

					if (applyChanges)
					{
						logger.LogInformation($"Saving file: {document.FilePath}");

						//var formattedRootNode = Formatter.Format(treeWithMigratedDac.GetRoot(), workspace);
						//SourceText newText = formattedRootNode.GetText();

						SourceText newText = treeWithMigratedDac.GetText();
						using (var writer = new StreamWriter(document.FilePath, append: false, encoding: text.Encoding))
						{
							newText.Write(writer);
						}
					}
				}
			}
		}


		if (!applyChanges)
		{
			logger.LogInformation("No changes were applied. Use --apply-changes to apply changes.");
			logger.LogInformation($"Total number of source files requiring modification: {filesWithChagnes.Count}");

		}
		else
		{
			logger.LogInformation("Changes were applied succefully.");
			logger.LogInformation($"Total number of source files modified: {filesWithChagnes.Count}");
		}

		if (!string.IsNullOrEmpty(reportFilePath))
		{
			ReportWriter.Write(reportFilePath, filesWithChagnes, logger);
		}

		return 0;
	}
}
