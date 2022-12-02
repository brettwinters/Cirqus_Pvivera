using System.Collections.Generic;
using System.Text;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Events;
using d60.Cirqus.Serialization;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Serialization
{
    [TestFixture]
    public class TestJsonDomainEventSerializer : FixtureBase
    {
        JsonDomainEventSerializer _serializer;

        protected override void DoSetUp()
        {
            _serializer = new JsonDomainEventSerializer();
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class Root : AggregateRoot { }

        [Test]
        public void CanRoundtripMyEvent()
        {
            var anEvent = new MyEvent
            {
                ListOfStuff = new List<string> { "hej", "med", "dig" }
            };

            var eventData = _serializer.Serialize(anEvent);
            var roundtrippedEvent = (MyEvent)_serializer.Deserialize(eventData);

            Assert.That(roundtrippedEvent.ListOfStuff, Is.EqualTo(new[] { "hej", "med", "dig" }));
        }

        [Test]
        public void UsesTypeAliasWhenProvided()
        {
	        //Arrange
	        _serializer.AddAliasesFor(typeof(MyEvent));
	        var anEvent = new MyEvent
            {
                ListOfStuff = new List<string> { "hej", "med", "dig" }
            };

            //Act
            var eventData = _serializer.Serialize(anEvent);

            //Assert
            var jsonDocument = System.Text.Json.JsonDocument.Parse(Encoding.UTF8.GetString(eventData.Data));
            Assert.AreEqual(
	            "MyEvent, <events>", 
	            jsonDocument.RootElement.GetProperty("$type").ToString()
	        );
        }

        private class MyGenericEvent<T> : DomainEvent<Root>
        {
	        public MyGenericEvent()
	        {
		        Meta["bim"] = "hej!";
	        }

	        public List<T> ListOfStuff { get; set; }
        }
        
        [Test]
        public void CanRoundtripGenericEvent()
        {
	        _serializer.AddAliasesFor(typeof(MyGenericEvent<string>));
	        var anEvent = new MyGenericEvent<string>()
	        {
		        ListOfStuff = new List<string> { "hej", "med", "dig" }
	        };

	        var eventData = _serializer.Serialize(anEvent);
	        var roundtrippedEvent = _serializer.Deserialize(eventData);
	        
	        Assert.IsInstanceOf<MyGenericEvent<string>>(roundtrippedEvent);

	        Assert.That(((MyGenericEvent<string>)roundtrippedEvent).ListOfStuff, Is.EqualTo(new[] { "hej", "med", "dig" }));
        }
        
        [Test]
        public void UsesTypeAliasWhenEventIsGenericProvided()
        {
	        //Arrange
	        _serializer.AddAliasesFor(typeof(MyGenericEvent<string>));
	        var anEvent = new MyGenericEvent<string>
            {
                ListOfStuff = new List<string> { "hej", "med", "dig" }
            };

	        var name = typeof(MyGenericEvent<>).Name;

            //Act
            var eventData = _serializer.Serialize(anEvent);

            //Assert
            var jsonDocument = System.Text.Json.JsonDocument.Parse(Encoding.UTF8.GetString(eventData.Data));
            Assert.AreEqual(
	            "MyGenericEvent`1, <events>", 
	            jsonDocument.RootElement.GetProperty("$type").ToString()
	        );
        }

        private class MyEvent : DomainEvent<Root>
        {
            public MyEvent()
            {
                Meta["bim"] = "hej!";
            }

            public List<string> ListOfStuff { get; set; }
        }

        [Test]
        public void CanRoundtripSimpleEvent()
        {
            var anEvent = new SimpleEvent
            {
                ListOfStuff = new List<string> { "hej", "med", "dig" }
            };

            var eventData = _serializer.Serialize(anEvent);
            var roundtrippedEvent = (SimpleEvent)_serializer.Deserialize(eventData);

            Assert.That(roundtrippedEvent.ListOfStuff, Is.EqualTo(new[] { "hej", "med", "dig" }));
        }

        private class SimpleEvent : DomainEvent<Root>
        {
            public List<string> ListOfStuff { get; set; }
        }

        [Test]
        public void CanRoundtripMostSimpleEvent()
        {
            var anEvent = new MostSimpleEvent { Text = "hej med dig" };

            var eventData = _serializer.Serialize(anEvent);
            var roundTrippedEvent = (MostSimpleEvent)_serializer.Deserialize(eventData);

            Assert.That(roundTrippedEvent.Text, Is.EqualTo("hej med dig"));
        }

        private class MostSimpleEvent : DomainEvent<Root>
        {
            public string Text { get; set; }
        }
    }
}