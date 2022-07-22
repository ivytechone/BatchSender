namespace IvyTech.Utilities
{
	public interface ITimeoutSource
	{
		Task WhenTimeout(int timeout, CancellationToken token);
	}
}
