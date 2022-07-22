using System.Collections.Concurrent;

namespace IvyTech.Utilities
{
	public class BatchSender<T> : IBatchSender<T>
	{
		private ITimeoutSource _timeoutSource;
		private ConcurrentQueue<T> _queue;
		private List<T> _batch;
		private readonly int _sendCount;
		private readonly int _sendDelay;
		private BatchSenderState _state;

		private bool _updated;

		private TaskCompletionSource _waitForMoreWorkSource;
		private TaskCompletionSource _waitForShutdownRequestSource;

		private Task _workerTask;

		public BatchSender(int sendCount, int sendDeplay, ITimeoutSource? timeoutSource = null)
		{
			_queue = new ConcurrentQueue<T>();
			_batch = new List<T>();
			_sendCount = sendCount;
			_sendDelay = sendDeplay;	
			_updated = false;
			_timeoutSource = timeoutSource is null  ? new TimeoutSource() : timeoutSource;

			_waitForMoreWorkSource = new TaskCompletionSource();
			_waitForShutdownRequestSource = new TaskCompletionSource();

			_workerTask = Task.Run(() => WorkerAsync());
			_state = BatchSenderState.Running;
		}

		public int QueueCount
		{
			get { return _queue.Count; }
		}

		public int BatchCount
		{
			get { return _batch.Count; }
		}

		public BatchSenderState State
		{
			get { return _state; }
		}

		public Action? OnUpdate { get; set; }

		public Func<IEnumerable<T>, Task<bool>>? Send { get; set; }

		public void Queue(T item)
		{
			if (_state != BatchSenderState.Running)
			{
				throw new InvalidOperationException("BatchSender is not in running state");
			}
			_queue.Enqueue(item);
			if (!_waitForMoreWorkSource.Task.IsCompleted)
			{
				_waitForMoreWorkSource.SetResult();
			}
			Update();
		}


		public void startShutdown()
		{
			_state = BatchSenderState.Stopping;
			_waitForShutdownRequestSource?.SetResult();
			Update();
		}

		public async Task WaitForShutdownComplete()
		{
			await _workerTask;
		}

		public async Task WorkerAsync()
		{
			DateTime? sendTime = null;
			bool sendTimeElapsed = false;
			Update();

			while (true)
			{
				bool queueEmpty = false;
				if (_queue.TryDequeue(out T? item))
				{
					if (_batch.Count == 0)
					{
						sendTime = DateTime.UtcNow.AddMilliseconds(_sendDelay);
					}
					_batch.Add(item);
					_updated = true;
				}
				else
				{
					queueEmpty = true;
				}

				if (queueEmpty == true && _batch.Count == 0 && _state == BatchSenderState.Stopping)
				{
					_state = BatchSenderState.Stopped;
					Update();
					return;
				}

				if (_batch.Count >= _sendCount || sendTimeElapsed || (queueEmpty && _state == BatchSenderState.Stopping))
				{
					queueEmpty = false;

					if (_updated) Update();

					if (Send is not null)
					{
						await Send(_batch);
					}

					_updated = true;
					_batch.Clear();
					sendTime = null;
					sendTimeElapsed = false;
					continue;
				}

				if (!queueEmpty || _state == BatchSenderState.Stopping)
				{
					continue;
				}

				sendTimeElapsed = await WhenNeedToResume(sendTime);
			}
		}

		private void Update()
		{
			_updated = false;
			if (OnUpdate is not null) OnUpdate();
		}
	
		private async Task WhenShuttingDown()
		{
			if (_waitForShutdownRequestSource is null) throw new ArgumentNullException("_waitForShutdownRequestSource");
			await _waitForShutdownRequestSource.Task;
		}

		private async Task WhenNewWork()
		{
			if (_waitForMoreWorkSource is null) throw new ArgumentNullException("_waitForMoreWorkSource"); 
			await _waitForMoreWorkSource.Task;
		}

		private int timeUntilExpiration(DateTime expirationTime)
		{
			var timeUntilSend = expirationTime - DateTime.UtcNow;
			return Convert.ToInt32(Math.Ceiling(timeUntilSend.TotalMilliseconds));
		}

		private async Task<bool> WhenNeedToResume(DateTime? expirationTime)
		{
			CancellationTokenSource timoutCancellationTokenSource = new();

			var forShutdown = WhenShuttingDown();
			var forNewWork = WhenNewWork();
			var forTimeout = expirationTime is not null ? _timeoutSource.WhenTimeout(timeUntilExpiration(expirationTime.Value), timoutCancellationTokenSource.Token) : null;
			bool didTimeout = false;

			if (_updated)
			{
				Update();
			}

			if (forTimeout is null)
			{
				await Task.WhenAny(new Task[] { forNewWork, forShutdown });
			}
			else
			{
				didTimeout = forTimeout == await Task.WhenAny(new Task[] { forTimeout, forNewWork, forShutdown });
			}

			if (forTimeout is not null && !forTimeout.IsCompleted)
			{
				timoutCancellationTokenSource.Cancel();
				await forTimeout;
			}

			if (!forNewWork.IsCompleted)
			{
				_waitForMoreWorkSource?.SetResult();
				await forNewWork;
			}

			if (!forShutdown.IsCompleted)
			{
				_waitForShutdownRequestSource?.SetResult();
				await forShutdown;
			}

			_waitForMoreWorkSource = new TaskCompletionSource();
			_waitForShutdownRequestSource = new TaskCompletionSource();

			return didTimeout;
		}
	}
}
