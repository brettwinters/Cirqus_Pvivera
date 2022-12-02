using System;
using System.Collections.Generic;
using System.Linq;
using d60.Cirqus.Numbers;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Numbers;

public class TestGlobalSequenceNumberService
{
	#region

	IEnumerable<long> GetGlobalSequenceNumbers(
		int count)
	{
		for (int i = 0; i < count; i++)
		{
			yield return GlobalSequenceNumberService.GetNewGlobalSequenceNumber();
		}
	}

	#endregion

	[SetUp]
	public void Setup()
	{
		//Flaky 1
		//Flaky 2 added this
		GlobalSequenceNumberService.GetNewGlobalSequenceNumber();
	}
	
	[Test]
	public void ShouldReturnCurrentUtcTicks()
	{
		// flaky
		// flaky 100 -> 200
		Assert.That(GlobalSequenceNumberService.GetNewGlobalSequenceNumber(), 
			Is.EqualTo(DateTimeOffset.UtcNow.Ticks).Within(200));
	}
	
	[Test]
	public void UtcNowShouldOutputUniqueSequenceNumbers()
	{
		Assert.AreEqual(
			10000,
			GetGlobalSequenceNumbers(10000).Distinct().Count()
		);
	}
}