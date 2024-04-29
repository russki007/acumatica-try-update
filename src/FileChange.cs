using Microsoft.CodeAnalysis.Text;

namespace russki007;

public class FileChange
{
	public int LineNumber { get; }

	public int CharNumber { get; }

	public string ChangeDescription { get; }

	public FileChange(LinePosition changePosition, string changeDescription)
	{
		// LinePosition is zero based so we need to increment to report numbers people expect.
		LineNumber = changePosition.Line + 1;
		CharNumber = changePosition.Character + 1;
		ChangeDescription = changeDescription;
	}
}

