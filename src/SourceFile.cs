using Microsoft.CodeAnalysis;

namespace russki007;

public record SourceFile
{
	public DocumentId DocumentId { get; }

	public string FileName { get; }

	public string? FilePath { get; }

	public string? ProjectFilePath { get; }

	public IEnumerable<FileChange> FileChanges { get; }

	public SourceFile(Document document, IEnumerable<FileChange> fileChanges)
	{
		DocumentId = document.Id;
		FileName = document.Name;
		FilePath = document.FilePath;
		FileChanges = fileChanges;
		ProjectFilePath = document.Project.FilePath;
	}
}

