using System;
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

	public JsonDomainEventSerializer()
		: this("<events>") {
	}

	public JsonDomainEventSerializer(string virtualNamespaceName) {
		_binder = new TypeAliasBinder(virtualNamespaceName);

		Settings = new JsonSerializerSettings {
			ContractResolver = new ContractResolver(),
			Binder = _binder.AddType(typeof(Metadata)),
			TypeNameHandling = TypeNameHandling.Objects,
			Formatting = Formatting.Indented,
			FloatFormatHandling = FloatFormatHandling.DefaultValue,
			FloatParseHandling = FloatParseHandling.Decimal,
			Converters = new List<JsonConverter> { new CustomDecimalJsonConverter() }
		};
	}

	public class CustomDecimalJsonConverter : JsonConverter
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

	//internal class NumberConverter : JsonConverter
	//{

	//    public override bool CanConvert(Type objectType) =>
	//           (objectType == typeof(decimal) || objectType == typeof(decimal?)


	//    //|| objectType == typeof(object)
	//    //|| objectType == typeof(double) || objectType == typeof(double?)
	//    //|| objectType == typeof(int) || objectType == typeof(int?)
	//    //|| objectType == typeof(long) || objectType == typeof(long?)
	//    //|| objectType == typeof(short) || objectType == typeof(short?)

	//    );

	//    #region Read Json / Deserialize

	//    public override bool CanRead => false;


	//    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
	//        var token = JToken.Load(reader);

	//        //preserve the scale : 0 -> 0m, 0.1m -> 0.1m, 0.10m -> 0.10m
	//        if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer) {
	//            var value = token.ToObject<decimal>();
	//            var scale = GetScale(value);
	//            var scaledValue = SetScale(value, scale);
	//            return scaledValue;
	//        }

	//        return serializer.Deserialize(reader);
	//    }
	//    //base.ReadJson(reader, objectType, existingValue, serializer);

	//    //if (token.Type == JTokenType.Null) {// && objectType == typeof(decimal?)) {
	//    //    return null;
	//    //}

	//    //can a string come in here?
	//    //if (token.Type == JTokenType.String) {
	//    //    if (string.IsNullOrEmpty(token.ToString())) {
	//    //        return null;
	//    //    }
	//    //    return decimal.Parse(token.ToString(), System.Globalization.CultureInfo.InvariantCulture);
	//    //}

	//    //throw new JsonSerializationException("Unexpected token type: " + token.Type.ToString());


	//    #endregion

	//    #region Write Json / Serialize

	//    public override bool CanWrite => true;

	//    /// <summary>
	//    /// serialize
	//    /// </summary>
	//    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {

	//        //just write it how it comes in
	//        //writer.WriteRawValue(value.ToString());

	//        writer.WriteValue(Convert.ToString(value));



	//    }

	//    #endregion

	//    private decimal? SetScale(decimal? value, int scale) {
	//        if (value == null) {
	//            return null;
	//        }
	//        return decimal.Round((decimal)value * decimal.Parse("1." + new string('0', scale)), scale);
	//    }

	//    private int GetScale(decimal? value) {
	//        if (value == null) {// || value == 0) {
	//            return 0;
	//        }
	//        var bits = decimal.GetBits((decimal)value);
	//        return (bits[3] >> 16) & 0x7F;
	//    }
	//}

	public class ContractResolver : DefaultContractResolver
	{
		protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) {
			var jsonProperties = base.CreateProperties(type, memberSerialization)
				.Where(property => property.DeclaringType != typeof(DomainEvent) && property.PropertyName != "Meta")
				.ToList();

			return jsonProperties;
		}
	}

	public JsonSerializerSettings Settings { get; private set; }

	public JsonDomainEventSerializer AddAliasFor(Type type) {
		_binder.AddType(type);
		return this;
	}

	public JsonDomainEventSerializer AddAliasesFor(params Type[] types) => AddAliasesFor((IEnumerable<Type>)types);

	public JsonDomainEventSerializer AddAliasesFor(IEnumerable<Type> types) {
		foreach (var type in types) {
			AddAliasFor(type);
		}
		return this;
	}

	public EventData Serialize(DomainEvent e) {
		try {
			var jsonText = JsonConvert.SerializeObject(e, Settings);
			var data = DefaultEncoding.GetBytes(jsonText);

			var result = EventData.FromDomainEvent(e, data);

			result.MarkAsJson();

			return result;
		}
		catch (Exception exception) {
			throw new SerializationException(string.Format("Could not serialize DomainEvent {0} into JSON! (headers: {1})", e, e.Meta), exception);
		}
	}

	public DomainEvent Deserialize(EventData e) {
		var meta = e.Meta.Clone();
		var text = DefaultEncoding.GetString(e.Data);

		try {
			var possiblyJObject = JsonConvert.DeserializeObject(text, Settings);

			if (possiblyJObject is JObject) {
			}

			var deserializedObject = (DomainEvent)possiblyJObject;
			deserializedObject.Meta = meta;
			return deserializedObject;
		}
		catch (Exception exception) {
			throw new SerializationException(string.Format("Could not deserialize JSON text '{0}' into proper DomainEvent! (headers: {1})", text, e.Meta), exception);
		}
	}
}