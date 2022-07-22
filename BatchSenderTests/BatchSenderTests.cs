using IvyTech.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IvyTech.Utilities.BatchSenderTests
{
	[TestClass]
	public class BatchSenderTests
	{
		private IBatchSender<string> _batchSender;
		private MockTimeoutSource _timeoutSource;

		public BatchSenderTests()
		{
			_timeoutSource = new MockTimeoutSource();
			_batchSender = new BatchSender<string>(5, 0, _timeoutSource);
		}

		[TestInitialize]
		public void TestInitialize()
		{
			_timeoutSource = new MockTimeoutSource();
			_batchSender = new BatchSender<string>(5, 0, _timeoutSource);
		}

		[TestMethod]
		public async Task QueueSingleItemWaitForTimeout()
		{
			bool success = false;
			TaskCompletionSource sendRecievedSource = new();
			int sendNumber = 0;

			_batchSender.Send = items =>
			{
				sendNumber++;

				if (sendNumber == 1 && items.Where(x => x == "Test1").Any())
				{
					success = true;
				}
				else
				{
					throw new Exception("Unexpected call to send");
				}

				sendRecievedSource.SetResult();
				return Task.FromResult(true);
			};

			_batchSender.Queue("Test1");
			_timeoutSource.SetTimeout();
			await sendRecievedSource.Task;

			Assert.IsTrue(success);
		}

		[TestMethod]
		public async Task QueueMultipleItemsSendImmediate()
		{
			bool success = false;
			TaskCompletionSource sendRecievedSource = new();
			int sendNumber = 0;

			_batchSender.Send = items =>
			{
				sendNumber++;

				if (sendNumber == 1 && items.ToList().Count == 5)
				{
					success = true;
				}
				else
				{
					throw new Exception("Unexpected call to send");
				}

				sendRecievedSource.SetResult();
				return Task.FromResult(true);
			};

			_batchSender.Queue("Test1");
			_batchSender.Queue("Test2");
			_batchSender.Queue("Test3");
			_batchSender.Queue("Test4");
			_batchSender.Queue("Test5");
			await sendRecievedSource.Task;

			Assert.IsTrue(success);
		}


		[TestMethod]
		public async Task QueueItemDuringSend()
		{
			bool send1Success = false;
			bool send2Success = false;
			int sendNumber = 0;

			TaskCompletionSource sendRecievedSource1 = new();
			TaskCompletionSource sendRecievedSource2 = new();

			_batchSender.Send = items =>
			{
				sendNumber++;

				if (sendNumber == 1)
				{
					_batchSender.Queue("Test6");
				}
				
				if (sendNumber == 1 && items.ToList().Count == 5)
				{
					send1Success = true;
					sendRecievedSource1.SetResult();
				}
				else if (sendNumber == 2 && items.Where(x => x == "Test6").Any())
				{
					send2Success = true;
					sendRecievedSource2.SetResult();
				}
				else
				{
					throw new Exception("Unexpected call to send");
				}

				return Task.FromResult(true);
			};

			_batchSender.Queue("Test1");
			_batchSender.Queue("Test2");
			_batchSender.Queue("Test3");
			_batchSender.Queue("Test4");
			_batchSender.Queue("Test5");
			await sendRecievedSource1.Task;

			_timeoutSource.SetTimeout();
			await sendRecievedSource2.Task;

			Assert.IsTrue(send1Success);
			Assert.IsTrue(send2Success);
		}

		[TestMethod]
		public async Task QueueSingleShutdown()
		{
			bool success = false;
			TaskCompletionSource sendRecievedSource = new();

			_batchSender.Send = items =>
			{
				if (items.Where(x => x == "Test1").Any())
				{
					success = true;
				}

				sendRecievedSource.SetResult();
				return Task.FromResult(true);
			};

			_batchSender.Queue("Test1");

			_batchSender.startShutdown();
			await _batchSender.WaitForShutdownComplete();

			await sendRecievedSource.Task;

			Assert.IsTrue(success);
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public async Task QueueSingleShutdownQueueMore()
		{
			bool success1 = false;
			bool success2 = false;
			TaskCompletionSource sendRecievedSource = new();

			_batchSender.Send = items =>
			{
				if (items.Where(x => x == "Test1").Any())
				{
					success1 = true;
				}
				else if (items.Where(x => x == "Test2").Any())
				{
					success2 = true;
				}
				else
				{
					throw new Exception("Unexpected call to send");
				}

				sendRecievedSource.SetResult();
				return Task.FromResult(true);
			};

			_batchSender.Queue("Test1");
			_batchSender.startShutdown();
			_batchSender.Queue("Test2");

			await _batchSender.WaitForShutdownComplete();
			await sendRecievedSource.Task;
			Assert.IsTrue(success1);
			Assert.IsFalse(success2);
		}
	}
}