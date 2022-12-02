using System;
using System.Collections.Concurrent;
using Newtonsoft.Json.Serialization;

namespace d60.Cirqus.Serialization;

public class TypeAliasBinder : DefaultSerializationBinder
{
	readonly ConcurrentDictionary<Type, string> _typeToName = new();
	readonly ConcurrentDictionary<string, Type> _nameToType = new();
	readonly string _specialAssemblyName;
	private readonly Func<Type, string> _typeToAliasFunction;

	public TypeAliasBinder(
		string virtualNamespaceName,
		Func<Type, string> typeToAliasFunction = null)
	{
		_specialAssemblyName = virtualNamespaceName;
		_typeToAliasFunction = typeToAliasFunction ?? (t => t.Name);
	}

	public TypeAliasBinder AddType(
		Type specialType)
	{
		var alias = _typeToAliasFunction(specialType);
		if (!_typeToName.ContainsKey(specialType) && _nameToType.ContainsKey(alias))
		{
			var errorMessage = $"Cannot add short alias for {specialType} because the alias {alias} has already been added for {_nameToType[alias]}";
			throw new InvalidOperationException(errorMessage);
		}

		_typeToName[specialType] = alias;
		_nameToType[alias] = specialType;

		return this;
	}

	public override void BindToName(
		Type serializedType, 
		out string assemblyName, 
		out string typeName)
	{
		if (_typeToName.TryGetValue(serializedType, out var customizedTypeName))
		{
			assemblyName = _specialAssemblyName;
			typeName = customizedTypeName;
			return;
		}

		base.BindToName(serializedType, out assemblyName, out typeName);
	}

	public override Type BindToType(
		string assemblyName, 
		string typeName)
	{
		if (assemblyName == _specialAssemblyName && 
		    _nameToType.TryGetValue(typeName, out var customizedType))
		{
			return customizedType;
		}

		return base.BindToType(assemblyName, typeName);
	}
}