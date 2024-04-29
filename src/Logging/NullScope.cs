namespace russki007;

internal class NullScope : IDisposable
{
	public static NullScope Instance { get; } = new NullScope();

	private NullScope()
	{
	}

	public void Dispose()
	{
	}
}
