using System;
using d60.Cirqus.Numbers;

namespace d60.Cirqus.Tests;

internal static class TimeMachine
{
	public static void Reset()
	{
		TimeService.Service = new FakeTimeService(DateTime.UtcNow);
	}

	class FakeTimeService : ITimeService
	{
		private DateTime _startTime;
		private readonly bool _driftSlightly;

		public FakeTimeService(
			DateTime startTime,
			bool driftSlightly = true)
		{
			_startTime = startTime;
			_driftSlightly = driftSlightly;
		}

		public DateTime UtcNow()
		{
			
			return _startTime = _driftSlightly ? _startTime.AddTicks(1000) : _startTime;
		}
	}

	public static void FixCurrentTimeTo(
		DateTime startTime, 
		bool driftSlightlyForEachCall = true)
	{
		if (startTime.Kind != DateTimeKind.Utc)
		{
			throw new ArgumentException($"DateTime {startTime} has kind {startTime.Kind} - it must be UTC!");
		}

		TimeService.Service = new FakeTimeService(
			startTime: startTime,
			driftSlightly: driftSlightlyForEachCall
		);
	}
}