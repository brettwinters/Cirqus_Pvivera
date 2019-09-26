using System;
using d60.Cirqus.Events;
using d60.Cirqus.Serialization;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Events
{
    [TestFixture, Description("Ensures that events can be properly serialized/deserialized")]
    public class TestSerialization
    {

        private static object[] NumberValues =
        {
            new object[]{ Decimal.MaxValue },
            new object[]{ Decimal.MinValue }
        };

        private static object[] FloatValues =
        {
            new object[]{ float.MaxValue },
            new object[]{ float.MinValue }
        };


        [Test]
        [TestCaseSource("NumberValues")]
        public void RoundtripEventWithBoxedDecimalFields(object value)
        {
            var serializer = new JsonDomainEventSerializer("<events>");
            //  .AddAliasesFor(typeof(NumberDomainEvent), typeof(ComplexValue));

            var rootId = Guid.NewGuid();
            var utcNow = DateTime.UtcNow;

            var e = new NumberDomainEvent() {
                Value = value,
                Meta =
                {
                    {DomainEvent.MetadataKeys.AggregateRootId, rootId.ToString()},
                    {DomainEvent.MetadataKeys.TimeUtc, utcNow.ToString("u")}
                }
            };

            var text = serializer.Serialize(e);

            Console.WriteLine(text);

            var roundtrippedEvent = (NumberDomainEvent)serializer.Deserialize(text);

            //Assert.That(roundtrippedEvent.Meta[DomainEvent.MetadataKeys.AggregateRootId], Is.EqualTo(rootId));
            //Assert.That(roundtrippedEvent.Meta[DomainEvent.MetadataKeys.TimeLocal], Is.EqualTo(utcNow.ToLocalTime()));
            //Assert.That(roundtrippedEvent.Meta[DomainEvent.MetadataKeys.TimeUtc], Is.EqualTo(utcNow));
            Assert.AreEqual(value?.ToString(), roundtrippedEvent.Value?.ToString());
        }

        class NumberDomainEvent : DomainEvent
        {
            public object Value { get; set; }
        }

        [Test]
        [TestCaseSource("FloatValues")]
        public void RoundtripEventWithFloatFields(float value)
        {
            var serializer = new JsonDomainEventSerializer("<events>");
            //  .AddAliasesFor(typeof(NumberDomainEvent), typeof(ComplexValue));

            var rootId = Guid.NewGuid();
            var utcNow = DateTime.UtcNow;

            var e = new DomainEvent<float>() {
                Value = value,
                Meta =
                {
                    {DomainEvent.MetadataKeys.AggregateRootId, rootId.ToString()},
                    {DomainEvent.MetadataKeys.TimeUtc, utcNow.ToString("u")}
                }
            };

            var text = serializer.Serialize(e);

            Console.WriteLine(text);

            var roundtrippedEvent = (DomainEvent<float>)serializer.Deserialize(text);

            //Assert.That(roundtrippedEvent.Meta[DomainEvent.MetadataKeys.AggregateRootId], Is.EqualTo(rootId));
            //Assert.That(roundtrippedEvent.Meta[DomainEvent.MetadataKeys.TimeLocal], Is.EqualTo(utcNow.ToLocalTime()));
            //Assert.That(roundtrippedEvent.Meta[DomainEvent.MetadataKeys.TimeUtc], Is.EqualTo(utcNow));
            Assert.AreEqual(value, roundtrippedEvent.Value);
        }

        class DomainEvent<T> : DomainEvent
        {
            public T Value { get; set; }
        }



        [Test]
        public void RoundtripEventWithReadonlyFields()
        {
            var serializer = new JsonDomainEventSerializer("<events>")
                .AddAliasesFor(typeof(ComplexDomainEvent), typeof(ComplexValue));

            var rootId = Guid.NewGuid();
            var utcNow = DateTime.UtcNow;

            var e = new ComplexDomainEvent("hello there", new ComplexValue(23)) {
                Meta =
                {
                    {DomainEvent.MetadataKeys.AggregateRootId, rootId.ToString()},
                    {DomainEvent.MetadataKeys.TimeUtc, utcNow.ToString("u")}
                }
            };

            var text = serializer.Serialize(e);

            Console.WriteLine(text);

            var roundtrippedEvent = (ComplexDomainEvent)serializer.Deserialize(text);

            //Assert.That(roundtrippedEvent.Meta[DomainEvent.MetadataKeys.AggregateRootId], Is.EqualTo(rootId));
            //Assert.That(roundtrippedEvent.Meta[DomainEvent.MetadataKeys.TimeLocal], Is.EqualTo(utcNow.ToLocalTime()));
            //Assert.That(roundtrippedEvent.Meta[DomainEvent.MetadataKeys.TimeUtc], Is.EqualTo(utcNow));
            Assert.That(roundtrippedEvent.Text, Is.EqualTo("hello there"));
            Assert.That(roundtrippedEvent.Value.Value, Is.EqualTo(23));
        }

        class ComplexDomainEvent : DomainEvent
        {
            public readonly string Text;
            public readonly ComplexValue Value;

            public ComplexDomainEvent(string text, ComplexValue value)
            {
                Text = text;
                Value = value;
            }
        }

        class ComplexValue
        {
            public readonly int Value;
            public ComplexValue(int value)
            {
                Value = value;
            }
        }
    }
}