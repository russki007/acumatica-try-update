using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;

namespace russki007;

class BaseTypeRewriter : CSharpSyntaxRewriter
{
	private SemanticModel Model { get; }

	public ILogger? Logger { get; set; }

	public bool ApplyChanges { get; set; }

	private readonly Action<FileChange>? _callback;

	public BaseTypeRewriter(SemanticModel model, bool applyChanges = false, Action<FileChange>? callback = null, ILogger? logger = null)
		: base(visitIntoStructuredTrivia: false)
	{
		Model = model;
		ApplyChanges = applyChanges;
		_callback = callback;
		Logger = logger;
	}

	
	public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax? node)
	{
		if (node == null)
			return null;

		var symbol = Model.GetDeclaredSymbol(node);

		// Check if the class implements IBqlTable
		if (symbol != null && symbol.Interfaces.Any(i => i.Name == "IBqlTable"))
		{
			if (symbol.BaseType?.Name != "PXBqlTable")
			{

				_callback?.Invoke(new FileChange(node.GetLocation().GetMappedLineSpan().StartLinePosition, symbol.Name));
				
				if (ApplyChanges)
				{
					// TODO: add an option to check if using directive PX.Data namespace exist,
					// and use non namespace qualified type PXBqlTable

					// Add BqlTableBase after the before base class
					var updatedBaseList = node.BaseList?.WithTypes(
						SyntaxFactory.SeparatedList(
							node.BaseList.Types.Insert(0,
								SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("PX.Data.PXBqlTable")))
					));

					// Update the class declaration with the new base fileChanges
					return node.WithBaseList(updatedBaseList);

				}
			}
		}

		return base.VisitClassDeclaration(node);
	}

	private SyntaxNode ApplyDocComment(SyntaxNode node, string? docCommentId)
	{
		if (docCommentId == null)
			return node;

		// Look up the comment text
		string docCommentText = "// TODO: Change to the new BQL base table";

		// Get the SyntaxTrivia for the comment
		SyntaxTree newTree = (CSharpSyntaxTree)CSharpSyntaxTree.ParseText(docCommentText);
		var newTrivia = newTree.GetRoot().GetLeadingTrivia();

		if (node.HasLeadingTrivia)
		{
			SyntaxTriviaList triviaList = node.GetLeadingTrivia();

			// Check to see if there are any existing doc comments
			var docComments = triviaList
					.Where(n => n.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || n.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
					.Select(t => t.GetStructure())
					.OfType<DocumentationCommentTriviaSyntax>()
					.ToList();

			// Append the doc comment (even if the API already has /// comments)
			node = node.InsertTriviaBefore(triviaList.First(), newTrivia);
		}
		else // no leading trivia
		{
			node = node.WithLeadingTrivia(newTrivia);
		}


		return node.WithAdditionalAnnotations(Simplifier.Annotation);
	}
}
