namespace d60.Cirqus.Tests.Contracts.Views.Models.GeneralViewManagerTest
{
    public class GenerateNewId : d60.Cirqus.Commands.Command<IdGenerator>
    {
        public GenerateNewId(string aggregateRootId)
            : base(aggregateRootId)
        {
        }

        public string IdBase { get; set; }

        public override void Execute(IdGenerator aggregateRoot)
        {
            aggregateRoot.GenerateNewId(IdBase);
        }
    }
}