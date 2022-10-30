using System;
using System.Threading;
using System.Xml.Schema;

namespace d60.Cirqus.Numbers;

public static class TimeService
{
	public static ITimeService Service { get; set; } = new DefaultTimeService();
	
	public static Func<DateTime> GetUtcNow => () => Service.UtcNow();

	private class DefaultTimeService : ITimeService
	{
		private static long _lastTimeStamp = DateTime.UtcNow.Ticks;

		public DateTime UtcNow() => GetUniqueUtcNow();

		private static Func<DateTime> GetUniqueUtcNow { get; } = () => new DateTime(UtcNowTicks);

		public static long UtcNowTicks
		{
			get
			{
				long original, newValue;
				do
				{
					original = _lastTimeStamp;
					long now = DateTime.UtcNow.Ticks;
					newValue = Math.Max(now, original + 1);
				} while (Interlocked.CompareExchange(ref _lastTimeStamp, newValue, original) != original);

				return newValue;
			}
		}
	}
}

// /// <summary>
// /// Gets the current time as it should be: in UTC :)
// /// </summary>
// public static class TimeService2
// {
// 	private static long _lastTimeStamp = DateTime.UtcNow.Ticks;
// 	private static Func<DateTime> _originalGetUtcNow = () => new DateTime(UtcNowTicks);
// 	
// 	
// 	public static DateTime UtcNow()
// 	{
// 		return GetUtcNow();
// 	}
//
// 	// 1 = 7, 10 = 6, 100 = 5, 1000 = 4
// 	// ffff = 10,000 of sec so 100 = 5 zeros
// 	// 
// 	//internal static Func<DateTime> OriginalGetUtcNow = () => DateTime.UtcNow.AddMilliseconds(1);
// 	
// 	//internal static Func<DateTime> GetUtcNow => _originalGetUtcNow;
//
// 	public static Func<DateTime> GetUtcNow
// 	{
// 		get => _originalGetUtcNow;
// 		set => _originalGetUtcNow = value;
// 	}
//
// 	public static void Reset()
// 	{
// 		GetUtcNow = _originalGetUtcNow;
// 	}
//
// 	private static long UtcNowTicks
// 	{
// 		get
// 		{
// 			long original, newValue;
// 			do
// 			{
// 				original = _lastTimeStamp;
// 				long now = DateTime.UtcNow.Ticks;
// 				newValue = Math.Max(now, original + 1);
// 			} while (Interlocked.CompareExchange(ref _lastTimeStamp, newValue, original) != original);
//
// 			return newValue;
// 		}
// 	}
// 	
// }