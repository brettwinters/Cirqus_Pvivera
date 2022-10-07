namespace d60.Cirqus.Tests.Bugs.ReplicationScenario
{
    public class IncrementCountingRoot : d60.Cirqus.Commands.Command<CountingRoot>
    {
        public IncrementCountingRoot(string aggregateRootId) : base(aggregateRootId)
        {
        }

        public override void Execute(CountingRoot aggregateRoot)
        {
            aggregateRoot.IncrementYourself();
        }
    }
}