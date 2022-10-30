using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using System.Collections.Generic;

namespace d60.Cirqus.Tests
{
    public class LongRunningTestCaseAttribute : TestCaseAttribute, ITestBuilder
    {
        public LongRunningTestCaseAttribute(params object[] arguments) : base(arguments) { }

        IEnumerable<TestMethod> ITestBuilder.BuildFrom(IMethodInfo method, Test suite) {
            var cases = base.BuildFrom(method, suite);

            if (TestCategories.IgnoreLongRunning != true) {
                return cases;
            }

            var modifiedCases = new List<TestMethod>();

            // There's only one test, but iterate to avoid making assumptions about implementation
            foreach (var test in cases) {
                if (test.RunState == RunState.Runnable) {
                    test.RunState = RunState.Ignored;
                    test.Properties.Set(PropertyNames.SkipReason, "Long-running test");
                }

                modifiedCases.Add(test);
            }

            return modifiedCases;
        }
    }
}
