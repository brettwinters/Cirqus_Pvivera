using d60.Cirqus.Numbers;

namespace d60.Cirqus.Tests;

public static class FakeGlobalSequenceNumberService
{
	/// <summary>
	/// The first number that will be generated is 0 unless overridden
	/// </summary>
	public static void Reset(
		long initialSequenceNumber = -1)
	{
		GlobalSequenceNumberService.Service = new GeneratesSequentialNumbers(initialSequenceNumber);
	}

	class GeneratesSequentialNumbers : IGlobalSequenceNumberService
	{
		private long _currentSequenceNumber;

		public GeneratesSequentialNumbers(
			long initialSequenceNumber = 0)
		{
			_currentSequenceNumber = initialSequenceNumber;
		}

		public long NewGlobalSequenceNumber => _currentSequenceNumber += 1;
	}
}