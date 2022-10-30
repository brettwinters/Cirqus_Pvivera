using System;
using System.Collections.Generic;
using System.Linq;
using d60.Cirqus.Numbers;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Numbers;

public class TestTime
{
	#region

	IEnumerable<DateTime> GetTimes(
		int count)
	{
		for (int i = 0; i < count; i++)
		{
			yield return TimeService.GetUtcNow();
		}
	}

	#endregion
	
	[Test]
	public void ShouldReturnCurrentUtcTime()
	{
		Assert.That(TimeService.GetUtcNow(), Is.EqualTo(DateTime.UtcNow).Within(1).Seconds);
	}
	
	[Test]
	public void UtcNowShouldOutputUniqueTimes()
	{
		Assert.AreEqual(
			10000,
			GetTimes(10000).Distinct().Count()
		);
	}
}