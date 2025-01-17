﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Commands;
using d60.Cirqus.Config;
using d60.Cirqus.Events;
using d60.Cirqus.InMemory.Events;
using d60.Cirqus.Tests.Extensions;
using NUnit.Framework;

namespace d60.Cirqus.Tests.Integration
{
    [TestFixture]
    public class TypeNameAliases : FixtureBase
    {
        ICommandProcessor _commandProcessor;
        Task<InMemoryEventStore> _eventStore;
        MyNameNameMapper _myNameNameMapper;

        [Test]
        public void SetsOwnerToGivenAlias()
        {
            _myNameNameMapper = new MyNameNameMapper();

            SetUpCommandProcessor(_myNameNameMapper);

            const string eventAlias = "customizzle event nizzle ";
            const string rootAlias = "customizzle aggregizzle nizlledizzle";

            _myNameNameMapper.AddAlias<OneRoot>(rootAlias);
            _myNameNameMapper.AddAlias<OneEvent>(eventAlias);

            var command = new GenericCommand(context =>
            {
                var oneRoot = context.TryLoad<OneRoot>("someId")
                              ?? context.Create<OneRoot>("someId");

                oneRoot.Bam();
            });

            _commandProcessor.ProcessCommand(command);

            var metadataOfEvent = _eventStore.Result.Single().Meta;

            Assert.That(metadataOfEvent[DomainEvent.MetadataKeys.Owner], Is.EqualTo(rootAlias));
            Assert.That(metadataOfEvent[DomainEvent.MetadataKeys.Type], Is.EqualTo(eventAlias));
        }

        [Test]
        public void SetsOwnerToGivenAliasWithAssemblyScannerThingie()
        {
            SetUpCommandProcessor(
                CustomizableDomainTypeNameMapper
                    .UseShortTypeNames()
                    .AddTypes(typeof(OneRoot), typeof(OneEvent))
                );

            var command = new GenericCommand(context =>
            {
                var oneRoot = context.TryLoad<OneRoot>("someId")
                              ?? context.Create<OneRoot>("someId");

                oneRoot.Bam();
            });

            _commandProcessor.ProcessCommand(command);

            var metadataOfEvent = _eventStore.Result.Single().Meta;

            Assert.That(metadataOfEvent[DomainEvent.MetadataKeys.Owner], Is.EqualTo("OneRoot"));
            Assert.That(metadataOfEvent[DomainEvent.MetadataKeys.Type], Is.EqualTo("OneEvent"));
        }

        #region

        void SetUpCommandProcessor(IDomainTypeNameMapper typeNameMapperToUse)
        {
	        _commandProcessor = CreateCommandProcessor(config => config
		        .EventStore(e => _eventStore = e.UseInMemoryEventStore())
		        .Options(o => o.UseCustomDomainTypeNameMapper(typeNameMapperToUse)));

	        RegisterForDisposal(_commandProcessor);
        }

        class OneRoot : AggregateRoot, IEmit<OneEvent>
        {
	        public void Bam()
	        {
		        Emit(new OneEvent());
	        }

	        public void Apply(OneEvent e)
	        {
	        }
        }

        class OneEvent : DomainEvent<OneRoot> { }

        class GenericCommand : ExecutableCommand
        {
	        readonly Action<ICommandContext> _action;

	        public GenericCommand(Action<ICommandContext> action)
	        {
		        _action = action;
	        }

	        public override void Execute(ICommandContext context)
	        {
		        _action(context);
	        }
        }
        
        #endregion
    }

    public class MyNameNameMapper : IDomainTypeNameMapper
    {
	    readonly ConcurrentDictionary<string, Type> _aliasToType = new();
	    readonly ConcurrentDictionary<Type, string> _typeToAlias = new();

	    public void AddAlias<TDomainType>(string alias)
	    {
		    var type = typeof(TDomainType);
		    _aliasToType[alias] = type;
		    _typeToAlias[type] = alias;
	    }

	    public Type GetType(string name)
	    {
		    try
		    {
			    return _aliasToType[name];

		    }
		    catch (Exception exception)
		    {
			    throw new ArgumentException($"Could not get type for {name}", exception);
		    }
	    }

	    public string GetName(Type type)
	    {
		    try
		    {
			    return _typeToAlias[type];

		    }
		    catch (Exception exception)
		    {
			    throw new ArgumentException($"Could not get name for {type}", exception);
		    }
	    }
    }
}