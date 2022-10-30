using System;
using System.Threading;

namespace d60.Cirqus.Numbers;

public static class GlobalSequenceNumberService
{
	public static IGlobalSequenceNumberService Service { get; set; } = new DefaultService();

	public static long GetNewGlobalSequenceNumber() => Service.NewGlobalSequenceNumber;

	private class DefaultService : IGlobalSequenceNumberService
	{
		private static long _lastTimeStamp = DateTimeOffset.UtcNow.Ticks;

		public long NewGlobalSequenceNumber
		{
			get
			{
				long original, newValue;
				do
				{
					original = _lastTimeStamp;
					long now = DateTimeOffset.UtcNow.Ticks;
					newValue = Math.Max(now, original + 1);
				} while (Interlocked.CompareExchange(ref _lastTimeStamp, newValue, original) != original);

				return newValue;
			}
		}
	}
}