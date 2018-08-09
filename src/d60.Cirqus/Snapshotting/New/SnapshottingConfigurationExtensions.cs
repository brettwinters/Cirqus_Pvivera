using System;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Config.Configurers;
using d60.Cirqus.Events;
using d60.Cirqus.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace d60.Cirqus.Snapshotting.New
{
    /// <summary>
    /// Configuration extensions for enabling aggregate root snapshots
    /// </summary>
    public static class SnapshottingConfigurationExtensions
    {
        /// <summary>
        /// Enables aggregate root snapshotting. When enabled, aggregate roots can be snapped by applying a <see cref="EnableSnapshotsAttribute"/> to them,
        /// using the <see cref="EnableSnapshotsAttribute.Version"/> property to leave old snapshots behind.
        /// </summary>
        public static void EnableSnapshotting(this OptionsConfigurationBuilder builder, Action<SnapshottingConfigurationBuilder> configureSnapshotting)
        {
            var snapshottingConfigurationBuilder = new SnapshottingConfigurationBuilder(builder);

            configureSnapshotting(snapshottingConfigurationBuilder);

            builder.Decorate<IAggregateRootRepository>((inner, ctx) =>
            {
                var eventStore = ctx.GetService<IEventStore>();
                var domainEventSerializer = ctx.GetService<IDomainEventSerializer>();
                var snapshotStore = ctx.GetService<ISnapshotStore>();

                var threshold = snapshottingConfigurationBuilder.PreparationThreshold;

                return new NewSnapshottingAggregateRootRepositoryDecorator(inner, eventStore, domainEventSerializer, snapshotStore, threshold);
            });
        }
    }
}