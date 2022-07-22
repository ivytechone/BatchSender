namespace IvyTech.Utilities
{
	public enum BatchSenderState
	{
		Running,
		Stopping,
		Stopped
	}

	public interface IBatchSender<T>
	{
		void Queue(T item);
		public void startShutdown();

		Task WaitForShutdownComplete();

		// Properties
		int QueueCount { get; }
		int BatchCount { get; }
		BatchSenderState State { get; }

		// Callbacks
		
		// Send is called when BatchSender has data to send
		Func<IEnumerable<T>, Task<bool>>? Send { get; set; }

		// OnUpdate is called anytime there is a change to Properties
		Action? OnUpdate { get; set; }
	}
}
