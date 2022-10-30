using System;
using System.Runtime.Serialization;
using d60.Cirqus.Extensions;

namespace d60.Cirqus.Exceptions;

[Serializable]
public class AggregateRootNotFoundException : ApplicationException
{
	public AggregateRootNotFoundException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}

	public AggregateRootNotFoundException(Type aggregateRootType, string id)
		: base($"Could not find aggregate root of type {aggregateRootType.GetPrettyName()} with ID {id}")
	{
	}
}