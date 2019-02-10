using System;
using d60.Cirqus.Commands;
using d60.Cirqus.Config.Decorators;
using d60.Cirqus.Events;
using d60.Cirqus.Exceptions;
using d60.Cirqus.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace d60.Cirqus.Config.Configurers
{
    public class OptionsConfigurationBuilder : NewConfigurationBuilder
    {
        public OptionsConfigurationBuilder(IRegistrar2 registrar) : base(registrar) { }

        /// <summary>
        /// Configures Cirqus to purge all views when initializing.
        /// </summary>
        public void PurgeExistingViews(bool purgeViewsAtStartup = false)
        {
            RegisterInstance<Action<Options>>(o => o.PurgeExistingViews = purgeViewsAtStartup, multi: true);
        }

        /// <summary>
        /// Registers the given exception type as a special "domain exception", which will be passed uncaught out from
        /// command processing. All other exceptions will be wrapped in a <see cref="CommandProcessingException"/>.
        /// </summary>
        public void AddDomainExceptionType<TException>() where TException : Exception
        {
            RegisterInstance<Action<Options>>(o => o.AddDomainExceptionType<TException>(), multi: true);
        }

        /// <summary>
        /// Registers the given domain even serializer to be used instead of the default <see cref="JsonDomainEventSerializer"/>.
        /// </summary>
        public void UseCustomDomainEventSerializer(IDomainEventSerializer domainEventSerializer)
        {
            RegisterInstance(domainEventSerializer);
        }

        /// <summary>
        /// Registers the given type name mapper to be used instead of the default <see cref="DefaultDomainTypeNameMapper"/>
        /// </summary>
        public void UseCustomDomainTypeNameMapper(IDomainTypeNameMapper domainTypeNameMapper)
        {
            RegisterInstance(domainTypeNameMapper);
        }

        /// <summary>
        /// Configures the number of retries to perform in the event that a <see cref="ConcurrencyException"/> occurs.
        /// </summary>
        public void SetMaxRetries(int maxRetries)
        {
            RegisterInstance<Action<Options>>(o => o.MaxRetries = maxRetries, multi: true);
        }

        /// <summary>
        /// Decorates the <see cref="ICommandMapper"/> pipeline with a command mapper that can use the given <see cref="CommandMappings"/>
        /// </summary>
        public void AddCommandMappings(CommandMappings mappings)
        {
            Decorate<ICommandMapper>((inner, ctx) => mappings.CreateCommandMapperDecorator(inner));
        }

        /// <summary>
        /// Decorates <see cref="ICommandProcessor"/> with one that automatically inserts the name of the executed command as a metadata
        /// element with the key <see cref="DomainEvent.MetadataKeys.CommandTypeName"/>, using the current <see cref="IDomainTypeNameMapper"/>
        /// to deliver the name.
        /// </summary>
        public void AddCommandTypeNameToMetadata()
        {
            Decorate<ICommandProcessor>((inner, ctx) => new CommandTypeNameCommandProcessorDecorator(inner, ctx.GetService<IDomainTypeNameMapper>()));
        }
    }
}