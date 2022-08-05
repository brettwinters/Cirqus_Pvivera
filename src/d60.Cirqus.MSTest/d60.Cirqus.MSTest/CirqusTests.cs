using d60.Cirqus.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Linq;
using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace d60.Cirqus.MSTest
{
	[DebuggerStepThrough]
    public class CirqusTests : CirqusTestsHarness
    {
        public TestContext TestContext { get; protected set; }

        [TestInitialize]
        public void SetupInternal() {
            Begin(new ConsoleWriter());
            Setup();
        }

        
        protected virtual void Setup() { }

        [TestCleanup]
        public void TeardownInternal() {
            Teardown();
            End(TestContext.CurrentTestOutcome != UnitTestOutcome.Passed); //brett - was UnitTestOutcome.Failed
        }

        protected virtual void Teardown() {        }

        [DebuggerHidden]
        protected override void Fail() => Assert.Fail();

        protected T FindAggregateRoot<T>(string id) where T : Aggregates.AggregateRoot
        {
            return Context.AggregateRoots.OfType<T>().SingleOrDefault(r => r.Id == id);
        }
    }
}