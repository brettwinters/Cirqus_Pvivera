using d60.Cirqus.Commands;

namespace d60.Cirqus.Tests.Contracts.Views.Models.ViewInstanceDeletion
{
    public class Undo : d60.Cirqus.Commands.Command<Root>
    {
        public Undo(string aggregateRootId) : base(aggregateRootId)
        {
        }

        public override void Execute(Root aggregateRoot)
        {
            aggregateRoot.Undo();
        }
    }
}