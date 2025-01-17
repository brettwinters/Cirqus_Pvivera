﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using d60.Cirqus.Events;
using d60.Cirqus.Extensions;
using d60.Cirqus.Numbers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace d60.Cirqus.Serialization;

public class JsonDomainEventSerializer : IDomainEventSerializer
{
	readonly TypeAliasBinder _binder;
	static readonly Encoding DefaultEncoding = Encoding.UTF8;

	public JsonDomainEventSerializer() : this("<events>") {
	}

	public JsonDomainEventSerializer(
		string virtualNamespaceName,
		Func<Type, string> typeToAliasFunction = null) 
	{
		_binder = new TypeAliasBinder(
			virtualNamespaceName: virtualNamespaceName, 
			typeToAliasFunction: typeToAliasFunction ?? (t => t.Name)
		);

		AddAliasFor(typeof(Metadata));
	
		Settings = new JsonSerializerSettings 
		{
			ContractResolver = new ContractResolver(),
			SerializationBinder = _binder,
			TypeNameHandling = TypeNameHandling.Objects,
			Formatting = Formatting.Indented,
			FloatFormatHandling = FloatFormatHandling.DefaultValue,
			FloatParseHandling = FloatParseHandling.Decimal,
			Converters = new List<JsonConverter> { new CustomDecimalJsonConverter() }
		};
	}

	private class CustomDecimalJsonConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
			=> (objectType == typeof(decimal) || objectType == typeof(decimal?));

		public override bool CanRead => false;

		//Deserialization
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) => throw new NotImplementedException();

		public override bool CanWrite => true;

		//Serialization - just write the decimal as raw value
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) 
			=> writer.WriteRawValue(value?.ToString());
	}

	private class ContractResolver : DefaultContractResolver
	{
		protected override IList<JsonProperty> CreateProperties(
			Type type, 
			MemberSerialization memberSerialization) 
		{
			var jsonProperties = base.CreateProperties(type, memberSerialization)
				.Where(property => property.DeclaringType != typeof(DomainEvent) && property.PropertyName != "Meta")
				.ToList();

			return jsonProperties;
		}
	}

	private JsonSerializerSettings Settings { get; set; }
	
	public JsonDomainEventSerializer AddAliasesFor(params Type[] types) => AddAliasesFor((IEnumerable<Type>)types);

	public JsonDomainEventSerializer AddAliasesFor(
		IEnumerable<Type> types) 
	{
		foreach (var type in types) {
			AddAliasFor(type);
		}
		return this;
	}
	
	JsonDomainEventSerializer AddAliasFor(Type type) {
		_binder.AddType(type);
		return this;
	}

	public EventData Serialize(
		DomainEvent e) 
	{
		try {
			var jsonText = JsonConvert.SerializeObject(e, Settings);
			var data = DefaultEncoding.GetBytes(jsonText);

			var result = EventData.FromDomainEvent(e, data);

			result.MarkAsJson();

			return result;
		}
		catch (Exception exception) {
			throw new SerializationException($"Could not serialize DomainEvent {e} into JSON! (headers: {e.Meta})", exception);
		}
	}

	public DomainEvent Deserialize(
		EventData e)
	{
		var meta = e.Meta.Clone();
		var text = DefaultEncoding.GetString(e.Data);
	
		try {
			var possiblyJObject = JsonConvert.DeserializeObject(text, Settings);

			if (possiblyJObject is null)
			{
				throw new NullReferenceException(nameof(DomainEvent));
			}
	
			if (possiblyJObject is JObject) {
				//Brett - nothing here?
			}

			var deserializedObject = (DomainEvent)possiblyJObject;
			deserializedObject.Meta = meta;
			return deserializedObject;
		}
		catch (Exception exception) {
			throw new SerializationException($"Could not deserialize JSON text '{text}' into proper DomainEvent! (headers: {e.Meta})", exception);
		}
	}
}