using System;
using System.Linq;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Snapshotting;
using d60.Cirqus.Snapshotting.New;
using MongoDB.Driver;

namespace d60.Cirqus.MongoDb.Snapshotting;

class MongoDbSnapshotStore : ISnapshotStore
{
	readonly Sturdylizer _sturdylizer = new Sturdylizer();
	readonly IMongoCollection<NewSnapshot> _snapshots;

	public MongoDbSnapshotStore(IMongoDatabase database, string collectionName)
	{
		_snapshots = database.GetCollection<NewSnapshot>(collectionName);

		// var indexKeys = IndexKeys
		// 	.Ascending("AggregateRootId")
		// 	.Ascending("Version")
		// 	.Descending("ValidFromGlobalSequenceNumber");

		//_snapshots.CreateIndex(indexKeys);
		
		_snapshots.Indexes.CreateOne(
			new CreateIndexModel<NewSnapshot>(
				Builders<NewSnapshot>.IndexKeys
					.Ascending(i => i.AggregateRootId)
					.Ascending(i => i.Version)
					.Descending(i => i.ValidFromGlobalSequenceNumber
				)
			)
		);
	}

	public Snapshot LoadSnapshot<TAggregateRoot>(string aggregateRootId, long maxGlobalSequenceNumber)
	{
		var snapshotAttribute = GetSnapshotAttribute<TAggregateRoot>();

		if (snapshotAttribute == null)
		{
			return null;
		}

		// var query = Query.And(
		// 	Query.EQ("AggregateRootId", aggregateRootId),
		// 	Query.EQ("Version", snapshotAttribute.Version),
		// 	Query.LT("ValidFromGlobalSequenceNumber", maxGlobalSequenceNumber));
		//
		// var matchingSnapshot = _snapshots
		// 	.Find(query)
		// 	.SetSortOrder(SortBy.Descending("ValidFromGlobalSequenceNumber"))
		// 	.SetLimit(1)
		// 	.FirstOrDefault();
		
		
		var filter = new FilterDefinitionBuilder<NewSnapshot>().And(
			new FilterDefinitionBuilder<NewSnapshot>().Eq(x => x.AggregateRootId, aggregateRootId),
			new FilterDefinitionBuilder<NewSnapshot>().Eq(x => x.Version, snapshotAttribute.Version),
			new FilterDefinitionBuilder<NewSnapshot>().Lt(x => x.ValidFromGlobalSequenceNumber, maxGlobalSequenceNumber)
		);

		var matchingSnapshot = _snapshots
			.Find(filter)
			.SortByDescending(x => x.ValidFromGlobalSequenceNumber)
			.Limit(1)
			.FirstOrDefault();

		if (matchingSnapshot == null)
		{
			return null;
		}

		try
		{
			var instance = _sturdylizer.DeserializeObject(matchingSnapshot.Data);
			UpdateTimeOfLastUsage(matchingSnapshot);
			return new Snapshot(matchingSnapshot.ValidFromGlobalSequenceNumber, instance);
		}
		catch(Exception)
		{
			return null;
		}
	}

	public void SaveSnapshot<TAggregateRoot>(string aggregateRootId, AggregateRoot aggregateRootInstance, long validFromGlobalSequenceNumber)
	{
		var snapshotAttribute = GetSnapshotAttribute(aggregateRootInstance.GetType());
		var info = new AggregateRootInfo(aggregateRootInstance);
		var serializedInstance = _sturdylizer.SerializeObject(info.Instance);
		
		_snapshots
			.WithWriteConcern(WriteConcern.Unacknowledged)
			.InsertOne(
			new NewSnapshot
			{
				Id = $"{aggregateRootId}/{info.SequenceNumber}",
				AggregateRootId = aggregateRootId,
				Data = serializedInstance,
				ValidFromGlobalSequenceNumber = validFromGlobalSequenceNumber,
				Version = snapshotAttribute.Version
			}
		);
	}

	public bool EnabledFor<TAggregateRoot>()
	{
		return GetSnapshotAttribute<TAggregateRoot>() != null;
	}

	void UpdateTimeOfLastUsage(NewSnapshot matchingSnapshot)
	{
		// var query = Query.EQ("Id", matchingSnapshot.Id);
		// var update = Update.Set("LastUsedUtc", DateTime.UtcNow);
		// _snapshots.Update(query, update, UpdateFlags.None, WriteConcern.Unacknowledged);
		_snapshots
			.WithWriteConcern(WriteConcern.Unacknowledged)
			.UpdateOne(
				x => x.Id == matchingSnapshot.Id, 
				Builders<NewSnapshot>.Update.Set(p => p.LastUsedUtc, DateTime.UtcNow)
			);
	}

	static EnableSnapshotsAttribute GetSnapshotAttribute<TAggregateRoot>()
	{
		return GetSnapshotAttribute(typeof(TAggregateRoot));
	}

	static EnableSnapshotsAttribute GetSnapshotAttribute(Type type)
	{
		return type
			.GetCustomAttributes(typeof (EnableSnapshotsAttribute), false)
			.Cast<EnableSnapshotsAttribute>()
			.FirstOrDefault();
	}

	class NewSnapshot
	{
		public NewSnapshot()
		{
			LastUsedUtc = DateTime.UtcNow;
		}
		public string Id { get; set; }
		public string AggregateRootId { get; set; }
		public string Data { get; set; }
		public long ValidFromGlobalSequenceNumber { get; set; }
		public int Version { get; set; }
		public DateTime LastUsedUtc { get; set; }
	}
}