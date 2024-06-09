using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.CommandLine;

namespace russki007;

static class Utils
{
	public static (bool isSolution, string workspacePath) FindWorkspace(string searchDirectory, string? workspacePath = null)
	{
		if (!string.IsNullOrEmpty(workspacePath))
		{
			if (!Path.IsPathRooted(workspacePath))
			{
				workspacePath = Path.GetFullPath(workspacePath, searchDirectory);
			}

			return Directory.Exists(workspacePath)
				? FindWorkspace(workspacePath!) 
				: FindFile(workspacePath!); 
		}

		var foundSolution = FindMatchingFile(searchDirectory, FindSolutionFiles, $"Multipl solution files found in the specifie directory '{searchDirectory}. Specify which to use with the workspace argument.");
		var foundProject = FindMatchingFile(searchDirectory, FindProjectFiles, $"Multipl project files found in the specifie directory '{searchDirectory}. Specify which to use with the workspace argument.");

		if (!string.IsNullOrEmpty(foundSolution) && !string.IsNullOrEmpty(foundProject))
		{
			throw new FileNotFoundException($"Both a project file and solution file found in '{searchDirectory}'. Specify which to use with the workspace argument.");
		}

		if (!string.IsNullOrEmpty(foundSolution))
		{
			return (true, foundSolution!); // IsNullOrEmpty is not annotated on .NET Core 2.1
		}

		if (!string.IsNullOrEmpty(foundProject))
		{
			return (false, foundProject!); // IsNullOrEmpty is not annotated on .NET Core 2.1
		}

		throw new FileNotFoundException($"Could not find valid project or solution file in '{searchDirectory}'. Specify which to use with the workspac argument.");
	}

	private static IEnumerable<string> FindSolutionFiles(string basePath) => Directory.EnumerateFileSystemEntries(basePath, "*.sln", SearchOption.TopDirectoryOnly);

	private static IEnumerable<string> FindProjectFiles(string basePath) => Directory.EnumerateFileSystemEntries(basePath, "*.*proj", SearchOption.TopDirectoryOnly);

	private static string? FindMatchingFile(string searchBase, Func<string, IEnumerable<string>> fileSelector, string multipleFilesFoundError)
	{
		if (!Directory.Exists(searchBase))
		{
			return null;
		}

		var files = fileSelector(searchBase).ToList();
		if (files.Count > 1)
		{
			throw new FileNotFoundException(string.Format(multipleFilesFoundError, searchBase));
		}

		return files.Count == 1
			? files[0]
			: null;
	}


	public static async Task<Workspace?> LoadWorkspaceAsync(
		string solutionOrProjectPath,
		bool logWorkspaceWarnings,
		ILogger logger,
		CancellationToken cancellationToken)
	{

		var (isSolution, workspaceFilePath) = FindWorkspace(solutionOrProjectPath,solutionOrProjectPath);


		var properties = new Dictionary<string, string>(StringComparer.Ordinal)
		{
                // This property ensures that XAML files will be compiled in the current AppDomain
                // rather than a separate one. Any tasks isolated in AppDomains or tasks that create
                // AppDomains will likely not work due to https://github.com/Microsoft/MSBuildLocator/issues/16.
                { "AlwaysCompileMarkupFilesInSeparateDomain", bool.FalseString },
		};

		var workspace = MSBuildWorkspace.Create(properties);
		
		if (isSolution)
		{
			await workspace.OpenSolutionAsync(workspaceFilePath, cancellationToken: cancellationToken).ConfigureAwait(false);
		}
		else
		{
			try
			{
				await workspace.OpenProjectAsync(workspaceFilePath, cancellationToken: cancellationToken).ConfigureAwait(false);
			}
			catch (InvalidOperationException)
			{
				logger.LogError("Could not format '{0}'. Format currently supports only C# and Visual Basic projects.", workspaceFilePath);
				workspace.Dispose();
				return null;
			}
		}


		LogWorkspaceDiagnostics(logger, logWorkspaceWarnings, workspace.Diagnostics);

		return workspace;

		static void LogWorkspaceDiagnostics(ILogger logger, bool logWorkspaceWarnings, ImmutableList<WorkspaceDiagnostic> diagnostics)
		{
			if (!logWorkspaceWarnings)
			{
				return;
			}

			foreach (var diagnostic in diagnostics)
			{
				if (diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
				{
					logger.LogError(diagnostic.Message);
				}
				else
				{
					logger.LogWarning(diagnostic.Message);
				}
			}
		}
	}

	private static (bool isSolution, string workspacePath) FindFile(string workspacePath)
	{
		var workspaceExtension = Path.GetExtension(workspacePath);
		var isSolution = workspaceExtension.Equals(".sln", StringComparison.OrdinalIgnoreCase) || workspaceExtension.Equals(".slnf", StringComparison.OrdinalIgnoreCase);
		var isProject = !isSolution
			&& workspaceExtension.EndsWith("proj", StringComparison.OrdinalIgnoreCase);
			

		if (!isSolution && !isProject)
		{
			throw new FileNotFoundException($"The file '{Path.GetFileName(workspacePath)}' does not appear to be a valid project or solution file.");
		}

		if (!File.Exists(workspacePath))
		{
			var message = isSolution
				? "The solution file '{0}' does not exist."
				: "The project file '{0}' does not exist.";

			throw new FileNotFoundException(string.Format(message, workspacePath));
		}

		return (isSolution, workspacePath);
	}


	public static bool TryLoadMSBuild([NotNullWhen(returnValue: true)] out string? msBuildPath)
	{
		try
		{
			// Get the global.json pinned SDK or latest instance.
			var msBuildInstance = Microsoft.Build.Locator.MSBuildLocator.QueryVisualStudioInstances()
				.Where(instance => instance.Version.Major >= 6)
				.FirstOrDefault();
			if (msBuildInstance is null)
			{
				msBuildPath = null;
				return false;
			}

			msBuildPath = Path.EndsInDirectorySeparator(msBuildInstance.MSBuildPath)
				? msBuildInstance.MSBuildPath
				: msBuildInstance.MSBuildPath + Path.DirectorySeparatorChar;

			Microsoft.Build.Locator.MSBuildLocator.RegisterMSBuildPath(msBuildPath);
			return true;
		}
		catch
		{
			msBuildPath = null;
			return false;
		}
	}


	public static string EnsureTrailingSlash(string path)
		=> !string.IsNullOrEmpty(path) &&
			path[^1] != Path.DirectorySeparatorChar
			? path + Path.DirectorySeparatorChar
			: path;

	public static Argument<string> DefaultToCurrentDirectory(this Argument<string> arg)
	{
		arg.SetDefaultValueFactory(() => EnsureTrailingSlash(Directory.GetCurrentDirectory()));
		return arg;
	}


	public static bool IsPlatformReferenced(Project project, string assemblyName)
	{
		var compilation = project.GetCompilationAsync().Result;

		if (compilation.ReferencedAssemblyNames.Any(a => a.Name.Equals(assemblyName)))
		{
			return true;
		}

		return false;
	}
}
