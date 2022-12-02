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
	    protected override void DoSetUp()
        {
            
        }

	    #region

	    // ReSharper disable once ClassNeverInstantiated.Local
	    private class Root : AggregateRoot { }
        
	    private class MyEvent : DomainEvent<Root>
	    {
		    public MyEvent()
		    {
			    Meta["bim"] = "hej!";
		    }

		    public List<string> ListOfStuff { get; set; }
	    }
        
	    private class SimpleEvent : DomainEvent<Root>
	    {
		    public List<string> ListOfStuff { get; set; }
	    }
        
	    private class MostSimpleEvent : DomainEvent<Root>
	    {
		    public string Text { get; set; }
	    }

	    #endregion
	    
	    [Test]
	    public void CanRoundtripMostSimpleEvent()
	    {
		    var serializer = new JsonDomainEventSerializer();
		    var anEvent = new MostSimpleEvent { Text = "hej med dig" };

		    var eventData = serializer.Serialize(anEvent);
		    var roundTrippedEvent = (MostSimpleEvent)serializer.Deserialize(eventData);

		    Assert.That(roundTrippedEvent.Text, Is.EqualTo("hej med dig"));
	    }
	    
        [Test]
        public void CanRoundtripMyEvent()
        {
	        var serializer = new JsonDomainEventSerializer();

	        var anEvent = new MyEvent
            {
                ListOfStuff = new List<string> { "hej", "med", "dig" }
            };

            var eventData = serializer.Serialize(anEvent);
            var roundtrippedEvent = (MyEvent)serializer.Deserialize(eventData);

            Assert.That(roundtrippedEvent.ListOfStuff, Is.EqualTo(new[] { "hej", "med", "dig" }));
        }

        [Test]
        public void UsesTypeAliasWhenProvided()
        {
	        var serializer = new JsonDomainEventSerializer();

	        //Arrange
	        serializer.AddAliasesFor(typeof(MyEvent));
	        var anEvent = new MyEvent
            {
                ListOfStuff = new List<string> { "hej", "med", "dig" }
            };

            //Act
            var eventData = serializer.Serialize(anEvent);

            //Assert
            var jsonDocument = System.Text.Json.JsonDocument.Parse(Encoding.UTF8.GetString(eventData.Data));
            Assert.AreEqual(
	            "MyEvent, <events>", 
	            jsonDocument.RootElement.GetProperty("$type").ToString()
	        );
            
            var roundtrippedEvent = (MyEvent)serializer.Deserialize(eventData);
            Assert.That(roundtrippedEvent.ListOfStuff, Is.EqualTo(new[] { "hej", "med", "dig" }));
        }

        [Test]
        public void SerialisesTypeAliasWithTypeNameFunctionWhenProvided()
        {
	        var serializer = new JsonDomainEventSerializer(
		        virtualNamespaceName: "my_namespace", 
		        typeToAliasFunction: t => "Special_" + t.Name
		    );
	        serializer.AddAliasesFor(typeof(MyEvent));
	        var anEvent = new MyEvent
            {
                ListOfStuff = new List<string> { "hej", "med", "dig" }
            };

            //Act
            var eventData = serializer.Serialize(anEvent);

            //Assert
            var jsonDocument = System.Text.Json.JsonDocument.Parse(Encoding.UTF8.GetString(eventData.Data));
            Assert.AreEqual(
	            $"Special_MyEvent, my_namespace", 
	            jsonDocument.RootElement.GetProperty("$type").ToString()
	        );
            
            var roundtrippedEvent = (MyEvent)serializer.Deserialize(eventData);

            Assert.That(roundtrippedEvent.ListOfStuff, Is.EqualTo(new[] { "hej", "med", "dig" }));
            Assert.That(roundtrippedEvent.Meta["bim"], Is.EqualTo("hej!"));
        }

        [Test]
        public void CanRoundtripSimpleEvent()
        {
	        var serializer = new JsonDomainEventSerializer();

	        var anEvent = new SimpleEvent
            {
                ListOfStuff = new List<string> { "hej", "med", "dig" }
            };

            var eventData = serializer.Serialize(anEvent);
            var roundtrippedEvent = (SimpleEvent)serializer.Deserialize(eventData);

            Assert.That(roundtrippedEvent.ListOfStuff, Is.EqualTo(new[] { "hej", "med", "dig" }));
        }
    }
}