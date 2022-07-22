namespace IvyTech.Utilities.BatchSenderTests
{
	internal class MockTimeoutSource : ITimeoutSource
	{
		private TaskCompletionSource _mockTimeoutSource;

		public MockTimeoutSource()
		{
			_mockTimeoutSource = new TaskCompletionSource();
		}

		public async Task WhenTimeout(int timeout, CancellationToken token)
		{
			while (!_mockTimeoutSource.Task.IsCompleted)
			{
				await Task.Delay(1);

				if (token.IsCancellationRequested)
				{
					return;
				}
			}
			_mockTimeoutSource = new TaskCompletionSource();
		}

		public void SetTimeout()
		{
			_mockTimeoutSource.SetResult();
		}
	}
}
