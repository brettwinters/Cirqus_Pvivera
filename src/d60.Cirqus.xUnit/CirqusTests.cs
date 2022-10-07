using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using d60.Cirqus.Testing;
using Xunit.Abstractions;

namespace d60.Cirqus.xUnit;

public class CirqusTests : CirqusTestsHarness, IDisposable
{
	public CirqusTests(ITestOutputHelper output)
	{
		Begin(new TestOutputWriter(output));
	}

	public void Dispose()
	{
		End(Marshal.GetExceptionCode() != 0);
	}

	[DebuggerHidden]
	protected override void Fail()
	{
		Xunit.Assert.False(true, "Assertion failed.");
	}
	
	protected T FindAggregateRoot<T>(string id) where T : Aggregates.AggregateRoot
	{
		return Context.AggregateRoots.OfType<T>().SingleOrDefault(r => r.Id == id);
	}
	
}