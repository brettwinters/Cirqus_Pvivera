namespace d60.Cirqus.Tests.Contracts.Views.Models.GeneralViewManagerTest
{
    public class EmitEvent : d60.Cirqus.Commands.Command<EventEmitter>
    {
        public EmitEvent(string aggregateRootId)
            : base(aggregateRootId)
        {
        }

        public override void Execute(EventEmitter aggregateRoot)
        {
            aggregateRoot.DoIt();
        }
    }
}