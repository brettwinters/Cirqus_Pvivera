using System;
using System.Linq;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Commands;
using d60.Cirqus.Events;
using d60.Cirqus.Serialization;
using d60.Cirqus.Views;
using Microsoft.Extensions.DependencyInjection;

namespace d60.Cirqus.Config.Configurers
{
    internal class CommandProcessorConfigurationBuilder : ILoggingAndEventStoreConfiguration, IOptionalConfiguration<ICommandProcessor>
    {
        private readonly NewConfigurationContainer _newContainer;
        private readonly IServiceCollection _services;

        public CommandProcessorConfigurationBuilder(IServiceCollection services)
        {
            _services = services;
            _newContainer =  new NewConfigurationContainer(services);

            FillInDefaults();
        }

        public IEventStoreConfiguration Logging(Action<LoggingConfigurationBuilder> configure)
        {
            configure(new LoggingConfigurationBuilder(_newContainer));
            return this;
        }

        public IOptionalConfiguration<ICommandProcessor> EventStore(Action<EventStoreConfigurationBuilder> configure)
        {
            configure(new EventStoreConfigurationBuilder(_newContainer));
            return this;
        }

        public IOptionalConfiguration<ICommandProcessor> AggregateRootRepository(Action<AggregateRootRepositoryConfigurationBuilder> configure)
        {
            configure(new AggregateRootRepositoryConfigurationBuilder(_newContainer));
            return this;
        }

        public IOptionalConfiguration<ICommandProcessor> EventDispatcher(Action<EventDispatcherConfigurationBuilder> configure)
        {
            configure(new EventDispatcherConfigurationBuilder(_services));
            return this;
        }

        public IOptionalConfiguration<ICommandProcessor> Options(Action<OptionsConfigurationBuilder> configure)
        {
            configure(new OptionsConfigurationBuilder(_newContainer));
            return this;
        }

        void FillInDefaults()
        {
            _services.AddScoped<ICommandProcessor>(context =>
            {
                var eventStore = context.GetService<IEventStore>();
                var aggregateRootRepository = context.GetService<IAggregateRootRepository>();
                var eventDispatcher = context.GetService<IEventDispatcher>();
                var serializer = context.GetService<IDomainEventSerializer>();
                var commandMapper = context.GetService<ICommandMapper>();
                var domainTypeMapper = context.GetService<IDomainTypeNameMapper>();

                var options = new Options();

                context.GetServices<Action<Options>>()
                    .ToList()
                    .ForEach(action => action(options));

                var commandProcessor = new CommandProcessor(eventStore, aggregateRootRepository, eventDispatcher, serializer, commandMapper, domainTypeMapper, options);

                commandProcessor.Initialize();

                return commandProcessor;
            });

            _services.AddScoped<IAggregateRootRepository>(context =>
                new DefaultAggregateRootRepository(
                    context.GetService<IEventStore>(),
                    context.GetService<IDomainEventSerializer>(),
                    context.GetService<IDomainTypeNameMapper>()));

            _services.AddScoped<IDomainEventSerializer, JsonDomainEventSerializer>();
            _services.AddScoped<IEventDispatcher, NullEventDispatcher>();
            _services.AddScoped<ICommandMapper, DefaultCommandMapper>();
            _services.AddScoped<IDomainTypeNameMapper, DefaultDomainTypeNameMapper>();
            _services.AddScoped<IConnectionStringHelper, ConnectionStringHelper>();
        }
    }
}