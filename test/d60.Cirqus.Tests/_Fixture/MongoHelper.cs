using System;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace d60.Cirqus.Tests.MongoDb
{
    public static class MongoHelper
    {
	    private static readonly string TestDbName = "mongotestdb";

        public static IMongoDatabase InitializeTestDatabase(
	        bool dropExistingDatabase = true)
        {

            var config = Configuration.Get();
            var connectionString = config.GetConnectionString(TestDbName);
            var url = new MongoUrl(connectionString);

            var databaseName = GetDatabaseName(url);
            var client = new MongoClient(url);
	        var database = client.GetDatabase(databaseName);

            Console.WriteLine("Using Mongo database '{0}'", databaseName);
            if (dropExistingDatabase)
            {
                Console.WriteLine("Dropping Mongo database '{0}'", databaseName);
                client.DropDatabase(databaseName);
            }

            return database;
        }

        private static string GetDatabaseName(MongoUrl url)
        {
            var databaseName = url.DatabaseName;

            var teamCityAgentNumber = Environment.GetEnvironmentVariable("tcagent");

            if (string.IsNullOrWhiteSpace(teamCityAgentNumber) || !int.TryParse(teamCityAgentNumber, out var number))
            {
                return databaseName;
            }

            return $"{databaseName}_{number}";
        }
    }
}