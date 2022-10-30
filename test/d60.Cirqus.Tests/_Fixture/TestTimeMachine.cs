using System;
using System.Collections.Generic;
using System.Linq;
using d60.Cirqus.Numbers;
using NUnit.Framework;

namespace d60.Cirqus.Tests._Fixture;

public class TestTimeMachine
{
	#region

	IEnumerable<DateTime> GetTimes()
	{
		for (int i = 0; i < 1000; i++)
		{
			yield return TimeService.GetUtcNow();
		}
	}

	#endregion
	
	[Test]
	public void GivenFirstPointInTime_WhenGetUtcNow_ThenOutputsFromFirstPointInTime()
	{
		var firstPointInTime = new DateTime(1979, 3, 1, 19, 9, 8, 765, DateTimeKind.Utc);
		
		TimeMachine.FixCurrentTimeTo(
			startTime: firstPointInTime, 
			driftSlightlyForEachCall: true
		);
	        
		Assert.AreEqual(
			new DateTime(1979, 3, 1, 19, 9, 8, DateTimeKind.Utc),
			GetTimes().Select(t => new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, t.Second)).Distinct().Single()
		);
	}

	[Test]
	public void GivenDriftSlightlyIsFalse_WhenGetUtcNow_ThenOutputSameTimes()
	{
		var firstPointInTime = new DateTime(1979, 3, 1, 19, 9, 8, 765, DateTimeKind.Utc);
		
		TimeMachine.FixCurrentTimeTo(
			startTime: firstPointInTime, 
			driftSlightlyForEachCall: false
		);
	        
		Assert.AreEqual(
			1,
			GetTimes().Distinct().Count()
		);
	}
	
	[Test]
	public void GivenDriftSlightlyIsTrue_WhenGetUtcNow_ThenOutputUniqueTimes()
	{
		var firstPointInTime = new DateTime(1979, 3, 1, 19, 9, 8, 765, DateTimeKind.Utc);
		
		TimeMachine.FixCurrentTimeTo(
			startTime: firstPointInTime, 
			driftSlightlyForEachCall: true
		);
	        
		Assert.AreEqual(
			1000,
			GetTimes().Distinct().Count()
		);
	}
	
	[Test]
	public void GivenFirstPointInTimeIsSet_AndReset_WhenGetUtcNow_ThenOutputsCurrentDate()
	{
		TimeMachine.FixCurrentTimeTo(
			startTime: new DateTime(1979, 3, 1, 19, 9, 8, 765, DateTimeKind.Utc), 
			driftSlightlyForEachCall: true
		);
		
		TimeMachine.Reset();

		Assert.That(TimeService.GetUtcNow(), Is.EqualTo(DateTime.UtcNow).Within(1).Seconds);
	}
}