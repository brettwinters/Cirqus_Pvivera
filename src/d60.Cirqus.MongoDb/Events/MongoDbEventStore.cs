using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using d60.Cirqus.Events;
using d60.Cirqus.Exceptions;
using d60.Cirqus.Extensions;
using d60.Cirqus.Numbers;
using MongoDB.Bson;
using MongoDB.Driver;

namespace d60.Cirqus.MongoDb.Events;

public class MongoDbEventStore : IEventStore
{
	const string GlobalSeqUniquenessIndexName = "EnsureGlobalSeqUniqueness";
	// const string TimestampUniquenessIndexName = "EnsureTimestampUniqueness";
	const string SeqUniquenessIndexName = "EnsureSeqUniqueness";
	const string EventsDocPath = "Events";
	const string MetaDocPath = "Meta";
	static readonly string SeqNoDocPath = $"{EventsDocPath}.SequenceNumber";
	static readonly string GlobalSeqNoDocPath = $"{EventsDocPath}.GlobalSequenceNumber";
	// static readonly string TimestampDocPath = $"{EventsDocPath}.Meta.{DomainEvent.MetadataKeys.TimeUtc}";
	static readonly string AggregateRootIdDocPath = $"{EventsDocPath}.AggregateRootId";
	readonly IMongoCollection<MongoEventBatch> _eventBatches;

	public MongoDbEventStore(
		IMongoDatabase database, 
		string eventCollectionName, 
		bool automaticallyCreateIndexes = true)
	{
		_eventBatches = database.GetCollection<MongoEventBatch>(eventCollectionName);

		if (automaticallyCreateIndexes)
		{
			_eventBatches.Indexes.CreateOne(
				new CreateIndexModel<MongoEventBatch>(
					Builders<MongoEventBatch>.IndexKeys.Ascending(GlobalSeqNoDocPath),
					new CreateIndexOptions
					{
						Name = GlobalSeqUniquenessIndexName,
						Unique = true
					} 
				)
			);
			_eventBatches.Indexes.CreateOne(
				new CreateIndexModel<MongoEventBatch>(
					Builders<MongoEventBatch>.IndexKeys.Combine(
						Builders<MongoEventBatch>.IndexKeys.Ascending(AggregateRootIdDocPath),
						Builders<MongoEventBatch>.IndexKeys.Ascending(SeqNoDocPath)),
					new CreateIndexOptions
					{
						Name = SeqUniquenessIndexName,
						Unique = true
					} 
				)
			);
			// _eventBatches.Indexes.CreateOne(
			// 	new CreateIndexModel<MongoEventBatch>(
			// 		Builders<MongoEventBatch>.IndexKeys.Ascending(TimestampDocPath),
			// 		new CreateIndexOptions
			// 		{
			// 			Name = TimestampUniquenessIndexName,
			// 			Unique = true
			// 		} 
			// 	)
			// );
		}
	}

	public IEnumerable<EventData> Stream(
		long globalSequenceNumber = 0)
	{
		var lowerGlobalSequenceNumber = globalSequenceNumber;

		while (true)
		{
			var lowerGlobalSequenceNumberInQuery = lowerGlobalSequenceNumber; //< avoid "access to modified closure"

			var filter = new FilterDefinitionBuilder<MongoEventBatch>().Gte(GlobalSeqNoDocPath, lowerGlobalSequenceNumber);
			var eventBatch = _eventBatches.Find(filter)
				.Sort(Builders<MongoEventBatch>.Sort.Ascending(GlobalSeqNoDocPath))
				.Limit(1000)
				.ToList()
				.SelectMany(b => b.Events.OrderBy(e => e.GlobalSequenceNumber))
				.Where(e => e.GlobalSequenceNumber >= lowerGlobalSequenceNumberInQuery)
				.Select(MongoEventToEvent);
			
			var hadEvents = false;
			var maxGlobalSequenceNumberInBatch = -1L;

			foreach (var e in eventBatch)
			{
				hadEvents = true;
				yield return e;
				maxGlobalSequenceNumberInBatch = e.GetGlobalSequenceNumber();
			}

			if (!hadEvents) break;

			lowerGlobalSequenceNumber = maxGlobalSequenceNumberInBatch + 1;
		}
	}

	public IEnumerable<EventData> Load(
		string aggregateRootId, 
		long firstSeq = 0)
	{
		var lowerSequenceNumber = firstSeq;
		while (true)
		{
			var eventFilter = new FilterDefinitionBuilder<MongoEvent>().And(
				new FilterDefinitionBuilder<MongoEvent>().Eq(e => e.AggregateRootId, aggregateRootId),
				new FilterDefinitionBuilder<MongoEvent>().Gte(e => e.SequenceNumber, lowerSequenceNumber)
			);

			var filter = new FilterDefinitionBuilder<MongoEventBatch>().ElemMatch(e => e.Events, eventFilter);

			var lowerSequenceNumberInQuery = lowerSequenceNumber;
			
			var eventBatch = _eventBatches
				.Find(filter)
				.Sort(Builders<MongoEventBatch>.Sort.Ascending(GlobalSeqNoDocPath))
				.Limit(1000)
				.ToList()
				.SelectMany(b => b.Events
					.Where(e => e.AggregateRootId == aggregateRootId)
					.OrderBy(e => e.SequenceNumber))
				.Where(e => e.SequenceNumber >= lowerSequenceNumberInQuery)
				.Select(MongoEventToEvent);
			
			var hadEvents = false;
			var maxSequenceNumberInBatch = -1L;

			foreach (var e in eventBatch)
			{
				hadEvents = true;
				yield return e;
				maxSequenceNumberInBatch = e.GetSequenceNumber();
			}

			if (!hadEvents) break;

			lowerSequenceNumber = maxSequenceNumberInBatch + 1;
		}
	}

	public long GetNextGlobalSequenceNumber()
	{
		//TODO Uncomment
		return GlobalSequenceNumberService.GetNewGlobalSequenceNumber();
		// var doc = _eventBatches
		// 	.Find(_ => true)
		// 	.As<BsonDocument>()
		// 	.Sort(Builders<MongoEventBatch>.Sort.Descending(GlobalSeqNoDocPath))
		// 	.Limit(1)
		// 	.SingleOrDefault();
		//
		// return doc == null
		// 	? 0
		// 	: doc[EventsDocPath].AsBsonArray
		// 		.Select(e => e[MetaDocPath][DomainEvent.MetadataKeys.GlobalSequenceNumber].ToInt64())
		// 		.Max() + 1;
	}

	public long GetLastGlobalSequenceNumber()
	{
		var doc = _eventBatches
			.Find(_ => true)
			.As<BsonDocument>()
			.Sort(Builders<MongoEventBatch>.Sort.Descending(GlobalSeqNoDocPath))
			.Limit(1)
			.SingleOrDefault();
		
		return doc == null
			? -1
			: doc[EventsDocPath].AsBsonArray
				.Select(e => e[MetaDocPath][DomainEvent.MetadataKeys.GlobalSequenceNumber].ToInt64())
				.Max();
	}

	public void Save(
		Guid batchId, 
		IEnumerable<EventData> events)
	{
		var batch = events.ToList();

		if (!batch.Any())
		{
			throw new InvalidOperationException($"Attempted to save batch {batchId}, but the batch of events was empty!");
		}

		//TODO uncomment
		//var nextGlobalSeqNo = GetNextGlobalSequenceNumber();

		foreach (var e in batch)
		{
			//TODO Uncomment
			//e.Meta[DomainEvent.MetadataKeys.GlobalSequenceNumber] = GetNextGlobalSequenceNumber().ToString(Metadata.NumberCulture);
			//e.Meta[DomainEvent.MetadataKeys.GlobalSequenceNumber] = (nextGlobalSeqNo++).ToString(Metadata.NumberCulture);
			e.Meta[DomainEvent.MetadataKeys.BatchId] = batchId.ToString();
		}

		EventValidation.ValidateBatchIntegrity(batchId, batch);

		try
		{
			_eventBatches.InsertOne(
				new MongoEventBatch
				{
					BatchId = batchId.ToString(),
					Events = batch
						.Select(b =>
						{
							var isJson = b.IsJson();

							return new MongoEvent
							{
								Meta = GetMetadataAsDictionary(b.Meta),
								Bin = isJson ? null : b.Data,
								Body = isJson ? GetBsonValue(b.Data) : null,
								SequenceNumber = b.GetSequenceNumber(),
								GlobalSequenceNumber = b.GetGlobalSequenceNumber(),
								AggregateRootId = b.GetAggregateRootId()
							};
						})
						.ToList()
				}
			);
		}
		catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
		{
			throw new ConcurrencyException(batchId, batch, ex);
		}
	}

	BsonValue GetBsonValue(
		byte[] data)
	{
		var json = Encoding.UTF8.GetString(data);
		var doc = BsonDocument.Parse(json);

		// recursively replace property names that begin with a $ - deep inside, we know that
		// it's probably only a matter of avoiding JSON.NET's $type properties
		ReplacePropertyPrefixes(doc, "$", "¤");

		return doc;
	}

	void ReplacePropertyPrefixes(
		BsonDocument doc, 
		string prefixToReplace, 
		string replacement)
	{
		foreach (var property in doc.ToList())
		{
			if (property.Name.StartsWith(prefixToReplace))
			{
				doc.Remove(property.Name);

				// since we know that it's most likely just about JSON.NET's $type property, we ensure
				// that the replaced element gets to be first (which is required by JSON.NET)
				doc.InsertAt(0, new BsonElement(replacement + property.Name.Substring(prefixToReplace.Length), property.Value));
			}

			if (property.Value.IsBsonDocument)
			{
				ReplacePropertyPrefixes(property.Value.AsBsonDocument, prefixToReplace, replacement);
				continue;
			}

			if (property.Value.IsBsonArray)
			{
				foreach (var bsonValue in property.Value.AsBsonArray)
				{
					if (bsonValue.IsBsonDocument)
					{
						ReplacePropertyPrefixes(bsonValue.AsBsonDocument, prefixToReplace, replacement);
					}
				}
			}
		}
	}

	Dictionary<string, string> GetMetadataAsDictionary(Metadata meta)
	{
		return meta.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
	}

	Metadata GetDictionaryAsMetadata(Dictionary<string, string> dictionary)
	{
		var metadata = new Metadata();
		foreach (var kvp in dictionary)
		{
			metadata[kvp.Key] = kvp.Value;
		}
		return metadata;
	}

	EventData MongoEventToEvent(
		MongoEvent e)
	{
		var meta = GetDictionaryAsMetadata(e.Meta);
		var data = e.Bin ?? GetBytesFromBsonValue(e.Body);

		return EventData.FromMetadata(meta, data);
	}

	byte[] GetBytesFromBsonValue(BsonValue body)
	{
		var doc = body.AsBsonDocument;

		// make sure to replace ¤ with $ again
		ReplacePropertyPrefixes(doc, "¤", "$");

		return Encoding.UTF8.GetBytes(doc.ToString());
	}
}