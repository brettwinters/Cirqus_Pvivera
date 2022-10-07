using System;
using d60.Cirqus.Logging;
using MongoDB.Driver;

namespace d60.Cirqus.MongoDb.Logging;

public class MongoDbLoggerFactory : CirqusLoggerFactory
{
	//readonly MongoCollection _logStatements;
	private readonly IMongoCollection<LogStatement> _logStatements;

	public MongoDbLoggerFactory(
		IMongoDatabase database, 
		string collectionName)
	{
		_logStatements = database.GetCollection<LogStatement>(collectionName);
	}

	public override Logger GetLogger(Type ownerType)
	{
		return new MongoDbLogger(_logStatements, ownerType);
	}

	class MongoDbLogger : Logger
	{
		readonly IMongoCollection<LogStatement> _logStatements;
		readonly Type _ownerType;

		public MongoDbLogger(IMongoCollection<LogStatement> logStatements, Type ownerType)
		{
			_logStatements = logStatements;
			_ownerType = ownerType;
		}

		public override void Debug(string message, params object[] objs)
		{
			Write(Level.Debug, SafeFormat(message, objs));
		}

		public override void Info(string message, params object[] objs)
		{
			Write(Level.Info, SafeFormat(message, objs));
		}

		public override void Warn(string message, params object[] objs)
		{
			Write(Level.Warn, SafeFormat(message, objs));
		}

		public override void Warn(Exception exception, string message, params object[] objs)
		{
			Write(Level.Warn, SafeFormat(message, objs), exception);
		}

		public override void Error(string message, params object[] objs)
		{
			Write(Level.Error, SafeFormat(message, objs));
		}

		public override void Error(Exception exception, string message, params object[] objs)
		{
			Write(Level.Error, SafeFormat(message, objs), exception);
		}

	

		void Write(Level level, string text, Exception exception = null)
		{
			try
			{
				// if (exception == null)
				// {
					
					_logStatements.InsertOne(
						new LogStatement()
						{
							level = level.ToString(),
							text = text,
							time = DateTime.Now,
							owner = _ownerType.FullName,
							exception = exception?.ToString()
						} 
						//WriteConcern.Unacknowledged
					);
				// }
				// else
				// {
				// 	_logStatements.InsertOne(
				// 		new LogStatement()
				// 	{
				// 		level = level.ToString(),
				// 		text = text,
				// 		time = DateTime.Now,
				// 		owner = _ownerType.FullName,
				// 		exception = exception.ToString()
				// 	}, WriteConcern.Unacknowledged);
				// }
			}
			catch { }
		}

		string SafeFormat(string message, object[] objs)
		{
			try
			{
				return string.Format(message, objs);
			}
			catch
			{
				return message;
			}
		}
	}
}

public class LogStatement
{
	public string level { get; set; } = string.Empty;
	public string text { get; set; } = string.Empty;
	public string owner { get; set; } = string.Empty;
	public string? exception { get; set; }
	public DateTime time { get; set; }
}