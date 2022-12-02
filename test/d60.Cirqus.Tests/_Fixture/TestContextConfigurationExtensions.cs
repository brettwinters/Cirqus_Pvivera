using System;
using d60.Cirqus.Config.Configurers;
using d60.Cirqus.Testing;

namespace d60.Cirqus.Tests;

public static class TestContextConfigurationExtensions
{
	/// <summary>
	/// Configures the TestContext to not wait for views to catch up
	/// </summary>
	public static void Asynchronous(
		this OptionsConfigurationBuilder builder)
	{
		builder.RegisterInstance<Action<TestContext>>(o => o.Asynchronous = true, multi: true);
	}
}