using System;
using System.Collections.Generic;
using d60.Cirqus.Views;
using d60.Cirqus.Views.ViewManagers;

namespace d60.Cirqus.Config.Configurers
{
    /// <summary>
    /// Configuration builder that allows for configuring a <see cref="ViewManagerEventDispatcher"/>
    /// </summary>
    public class ViewManagerEventDispatcherConfigurationBuilder : NewConfigurationBuilder<ViewManagerEventDispatcher>
    {
        /// <summary>
        /// Creates the builder
        /// </summary>
        public ViewManagerEventDispatcherConfigurationBuilder(IRegistrar2 registrar) : base(registrar) {}

        /// <summary>
        /// Uses the given wait handle in the view dispatcher, allowing you to wait for specific views (or all views) to catch up to a certain state
        /// </summary>
        public ViewManagerEventDispatcherConfigurationBuilder WithWaitHandle(ViewManagerWaitHandle handle)
        {
            RegisterInstance(handle);
            return this;
        }

        /// <summary>
        /// Makes the given dictionary of items available in the <see cref="IViewContext"/> passed to the view's
        /// locator and the view itself
        /// </summary>
        public ViewManagerEventDispatcherConfigurationBuilder WithViewContext(IDictionary<string, object> viewContextItems)
        {
            if (viewContextItems == null) throw new ArgumentNullException("viewContextItems");
            RegisterInstance(viewContextItems);
            return this;
        }

        /// <summary>
        /// Configures the event dispatcher to persist its state after <paramref name="max"/> events at most
        /// </summary>
        public ViewManagerEventDispatcherConfigurationBuilder WithConfiguration(ViewManagerEventDispatcherConfiguration configuration)
        {
            RegisterInstance(configuration);
            return this;
        }

        /// <summary>
        /// Registers the given profiler with the event dispatcher, allowing you to aggregate timing information from the view subsystem
        /// </summary>
        public ViewManagerEventDispatcherConfigurationBuilder WithProfiler(IViewManagerProfiler profiler)
        {
            RegisterInstance(profiler);
            return this;
        }

        /// <summary>
        /// Enables the automatic view manager distribution service which periodically ensures that views are relatively fairly distributed
        /// among the available processes.
        /// </summary>
        [Obsolete("Please note that the AutomaticallyRedistributeViews function has not been tested yet")]
        public ViewManagerEventDispatcherConfigurationBuilder AutomaticallyRedistributeViews(string id, IAutoDistributionState autoDistributionState)
        {
            RegisterInstance(new AutoDistributionViewManagerEventDispatcher(id, autoDistributionState));
            return this;
        }
    }

    public class ViewManagerEventDispatcherConfiguration
    {
        public int MaxDomainEventsPerBatch { get; }

        public ViewManagerEventDispatcherConfiguration(int maxDomainEventsPerBatch)
        {
            MaxDomainEventsPerBatch = maxDomainEventsPerBatch;
        }
    }
}