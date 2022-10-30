

namespace d60.Cirqus.Testing;

[TestFixture]
public class TestCirqusTestsOverrideTestContext : CirqusTests
{
	FunnySerializer serializer;

	protected override void Setup()
	{
		serializer = new FunnySerializer();
		Configure(context => context.Options(x => x.UseCustomDomainEventSerializer(serializer)));
	}

	[Test]
	public void CanOverrideTestContests()
	{
		var @event = new Event();
		Emit(NewId<Root>(), @event);
		Assert.AreEqual(@event, serializer.VipEvent);
	}

	public class Event : DomainEvent<Root> {}
	public class Root : AggregateRoot { }

	public class FunnySerializer : IDomainEventSerializer
	{
		public DomainEvent VipEvent { get; private set; }
            
		public EventData Serialize(DomainEvent e)
		{
			VipEvent = e;
			return EventData.FromDomainEvent(e, new byte[0]);
		}

		public DomainEvent Deserialize(EventData e)
		{
			return VipEvent;
		}
	}
}