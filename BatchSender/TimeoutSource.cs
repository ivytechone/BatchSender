namespace IvyTech.Utilities
{
	public class TimeoutSource : ITimeoutSource
	{
		public async Task WhenTimeout(int timeout, CancellationToken token)
		{
			try
			{
				await Task.Delay(timeout, token);
			}
			catch (TaskCanceledException)
			{
			}
		}
	}
}
