using d60.Cirqus.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

namespace d60.Cirqus.MSTest
{
    public class CirqusTests : CirqusTestsHarness
    {
        public TestContext TestContext { get; protected set; }

        [TestInitialize]
        public void SetupInternal() {
            Begin(new ConsoleWriter());
            Setup();
        }

        [DebuggerStepThrough]
        protected virtual void Setup() { }

        [TestCleanup]
        public void TeardownInternal() {
            Teardown();
            End(TestContext.CurrentTestOutcome != UnitTestOutcome.Passed); //brett - was UnitTestOutcome.Failed
        }

        protected virtual void Teardown() {        }

        [DebuggerStepThrough]
        protected override void Fail() => Assert.Fail();
    }
}