using System;

namespace d60.Cirqus.Config;

/// <summary>
/// Implementation of <see cref="IDomainTypeNameMapper"/> that uses assembly-qualified
/// type names without version and culture information
/// </summary>
public class DefaultDomainTypeNameMapper : IDomainTypeNameMapper
{
	public Type GetType(string name)
	{
		var type = Type.GetType(name);

		if (type == null)
		{
			throw new ArgumentException($"Could not get aggregate root type from '{name}'");
		}

		return type;
	}

	public string GetName(Type type)
	{
		return $"{type.FullName}, {type.Assembly.GetName().Name}";
	}
}