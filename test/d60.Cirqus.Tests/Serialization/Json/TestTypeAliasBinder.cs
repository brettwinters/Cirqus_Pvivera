using d60.Cirqus.Serialization;
using Newtonsoft.Json;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Serialization;

[TestFixture]
public class TestTypeAliasBinder : FixtureBase
{
	TypeAliasBinder _binder;

	protected override void DoSetUp()
	{
		
	}

	class FakeSimpleType
	{
	}
	
	/*
	 * BindToName
	 */

	[Test]
	public void GivenNoAliasIsSpecified_WhenBindToName_ThenReturnsAssemblyFullName_AndTypeFullName()
	{
		_binder = new TypeAliasBinder("some_namespace");
		
		_binder.BindToName(
			typeof(FakeSimpleType), 
			out var assemblyName, 
			out var typeName
		);

		Assert.AreEqual(
			expected: typeof(FakeSimpleType).Assembly.FullName,
			actual: assemblyName
		);

		Assert.AreEqual(
			expected: typeof(FakeSimpleType).FullName,
			actual: typeName
		);
	}

	[Test]
	public void GivenAliasIsSpecified_AndTypeToAliasFunctionIsNotSpecified_WhenBindToName_ThenReturnsCustomAssembly_AndTypeName()
	{
		_binder = new TypeAliasBinder("some_namespace");

		_binder.AddType(typeof(FakeSimpleType));
		
		_binder.BindToName(
			typeof(FakeSimpleType), 
			out var assemblyName, 
			out var typeName
		);

		Assert.AreEqual(
			expected: "some_namespace",
			actual: assemblyName
		);

		Assert.AreEqual(
			expected: nameof(FakeSimpleType),
			actual: typeName
		);
	}

	[Test]
	public void GivenAliasIsSpecified_AndTypeToAliasFunctionIsSpecified_WhenBindToName_ThenReturnsCustomAssembly_AndTypeName()
	{
		_binder = new TypeAliasBinder(
			virtualNamespaceName: "some_namespace",
			typeToAliasFunction: _ => "some_alias"
		);

		_binder.AddType(typeof(FakeSimpleType));
		
		_binder.BindToName(
			typeof(FakeSimpleType), 
			out var assemblyName, 
			out var typeName
		);

		Assert.AreEqual(
			expected: "some_namespace",
			actual: assemblyName
		);

		Assert.AreEqual(
			expected: "some_alias",
			actual: typeName
		);
	}
	
	/*
	 * BindToType
	 */
	
	[Test]
	public void GivenAssemblyAndFullNameAreSpecified_AndNoAliasMatches_WhenBindToType_ThenReturnsType()
	{
		_binder = new TypeAliasBinder("some_namespace");
		
		var result = _binder.BindToType(
			typeof(FakeSimpleType).Assembly.FullName!, 
			typeof(FakeSimpleType).FullName!
		);

		Assert.AreEqual(
			expected: typeof(FakeSimpleType),
			actual: result
		);
	}
	
	[TestCase("another_namespace", nameof(FakeSimpleType))]
	[TestCase("my_namespace", "some_other_type")]
	public void GivenTypeToAliasNotSpecified_AndAssemblyNameDoesNotMatchSpecialAssemblyOrTypeMatchesName_WhenBindToType_ThenThrowsJsonSerializationException(
		string assemblyName,
		string typeName)
	{
		_binder = new TypeAliasBinder("my_namespace");

		Assert.Throws<JsonSerializationException>(() => 
			_binder.BindToType(
				"some_namespace", 
				nameof(FakeSimpleType)
			)
		);
	}
	
	[Test]
	public void GivenNoTypeToAliasIsSpecified_AndAliasIsSpecified_WhenBindToType_AndAliasMatchesName_ThenReturnsType()
	{
		_binder = new TypeAliasBinder("my_namespace");
		_binder.AddType(typeof(FakeSimpleType));

		var result = _binder.BindToType(
			assemblyName: "my_namespace", 
			typeName: nameof(FakeSimpleType)
		);

		Assert.AreEqual(
			expected: typeof(FakeSimpleType),
			actual: result
		);
	}
	
	[Test]
	public void GivenTypeToAliasIsSpecified_AndAliasIsSpecified_WhenBindToType_AndAliasMatchesName_ThenReturnsType()
	{
		_binder = new TypeAliasBinder(
			virtualNamespaceName: "my_namespace",
			typeToAliasFunction: _ => "my_alias"
		);
		
		_binder.AddType(typeof(FakeSimpleType));

		var result = _binder.BindToType(
			assemblyName: "my_namespace", 
			typeName: "my_alias"
		);

		Assert.AreEqual(
			expected: typeof(FakeSimpleType),
			actual: result
		);
	}
}