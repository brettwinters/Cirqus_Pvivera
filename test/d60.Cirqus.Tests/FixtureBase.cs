using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Timers;
using d60.Cirqus.Config.Configurers;
using d60.Cirqus.Extensions;
using d60.Cirqus.Logging;
using d60.Cirqus.Logging.Console;
using d60.Cirqus.Numbers;
using d60.Cirqus.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace d60.Cirqus.Tests
{
    public class FixtureBase : IDisposable
    {
        List<IDisposable> _stuffToDispose;

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            TimeMachine.Reset();
        }

        public FixtureBase()
        {
            _stuffToDispose = new List<IDisposable>();

            CirqusLoggerFactory.Current = new ConsoleLoggerFactory(minLevel: Logger.Level.Debug);
        }

        [SetUp]
        public void SetUp()
        {
            _stuffToDispose = new List<IDisposable>();

            CirqusLoggerFactory.Current = new ConsoleLoggerFactory(minLevel: Logger.Level.Debug);

            DoSetUp();
        }

        protected ICommandProcessor CreateCommandProcessor(Action<ILoggingAndEventStoreConfiguration> configure)
        {
            var services = new ServiceCollection();
            services.AddCirqus(configure.Invoke);

            var provider = services.BuildServiceProvider();
            return provider.GetService<ICommandProcessor>();
        }

        protected Cirqus.Testing.TestContext CreateTestContext(Action<IOptionalConfiguration<Cirqus.Testing.TestContext>> configure = null)
        {
            var services = new ServiceCollection();
            services.AddTestContext(configure);

            var provider = services.BuildServiceProvider();
            return provider.GetService<Cirqus.Testing.TestContext>();
        }

        protected void SetLogLevel(Logger.Level newLogLevel)
        {
            CirqusLoggerFactory.Current = new ConsoleLoggerFactory(minLevel: newLogLevel);
        }

        protected TDisposable RegisterForDisposal<TDisposable>(TDisposable disposable) where TDisposable : IDisposable
        {
            _stuffToDispose.Add(disposable);
            return disposable;
        }

        [TearDown]
        public void TearDown()
        {
            DoTearDown();

            DisposeStuff();
        }

        protected void DisposeStuff()
        {
            _stuffToDispose.ForEach(d => d.Dispose());
            _stuffToDispose.Clear();
        }

        protected virtual void DoSetUp()
        {
        }
        protected virtual void DoTearDown()
        {
        }

        public delegate void TimerCallback(TimeSpan elapsedTotal);

        protected void TakeTime(string description, Action action, TimerCallback periodicCallback = null)
        {
            Console.WriteLine("Begin: {0}", description);

            var stopwatch = Stopwatch.StartNew();
            var lastCallback = DateTime.UtcNow;

            using (var timer = new Timer())
            {
                if (periodicCallback != null)
                {
                    timer.Interval = 5000;
                    timer.Elapsed += delegate
                    {
                        periodicCallback(stopwatch.Elapsed);
                    };
                    timer.Start();
                }

                action();
            }
            var elapsed = stopwatch.Elapsed;
            Console.WriteLine("End: {0} - elapsed: {1:0.0} s", description, elapsed.TotalSeconds);
        }

        protected async Task TakeTimeAsync(string description, Func<Task> action, TimerCallback periodicCallback = null)
        {
            Console.WriteLine("Begin: {0}", description);

            var stopwatch = Stopwatch.StartNew();

            using (var timer = new Timer())
            {
                if (periodicCallback != null)
                {
                    timer.Interval = 5000;
                    timer.Elapsed += delegate
                    {
                        periodicCallback(stopwatch.Elapsed);
                    };
                    timer.Start();
                }

                await action();
            }
            var elapsed = stopwatch.Elapsed;
            Console.WriteLine("End: {0} - elapsed: {1:0.0} s", description, elapsed.TotalSeconds);
        }

        public void Dispose()
        {
            DisposeStuff();
        }
    }
}