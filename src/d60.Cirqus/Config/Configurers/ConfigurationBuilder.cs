using System;

namespace d60.Cirqus.Config.Configurers
{
    /// <summary>
    /// Configuration builder that is used to register factory methods for various services
    /// </summary>
    public abstract class NewConfigurationBuilder
    {
        protected IRegistrar2 Registrar;

        /// <summary>
        /// Constructs the builder
        /// </summary>
        protected NewConfigurationBuilder(IRegistrar2 registrar)
        {
            Registrar = registrar;
        }

        /// <summary>
        /// Registers a factory method for <typeparamref name="TService"/>
        /// </summary>
        public void Register<TService>(Func<IServiceProvider, TService> serviceFactory) where TService : class 
        {
            Registrar.Register(serviceFactory);
        }

        /// <summary>
        /// Registers a specific instance (which by definition is not a decorator) for <typeparamref name="TService"/>
        /// </summary>
        public void RegisterInstance<TService>(TService instance, bool multi = false) where TService : class 
        {
            Registrar.RegisterInstance(instance, multi);
        }

        /// <summary>
        /// Registers a factory method for decorating <typeparamref name="TService"/>
        /// </summary>
        public void Decorate<TService>(Func<TService, IServiceProvider, TService> serviceFactory) where TService : class
        {
            Registrar.Decorate(serviceFactory);
        }
    }

    public abstract class NewConfigurationBuilder<TService> : NewConfigurationBuilder
    {
        /// <summary>
        /// Constructs the builder
        /// </summary>
        protected NewConfigurationBuilder(IRegistrar2 registrar) : base(registrar) { }

        /// <summary>
        /// Registers a factory method for <typeparamref name="TService"/>
        /// </summary>
        public void Register<TService>(Func<IServiceProvider, TService> serviceFactory) where TService : class
        {
            Registrar.Register(serviceFactory);
        }

        /// <summary>
        /// Registers a specific instance (which by definition is not a decorator) for <typeparamref name="TService"/>
        /// </summary>
        public void RegisterInstance<TService>(TService instance, bool multi = false) where TService : class
        {
            Registrar.RegisterInstance(instance, multi);
        }

        /// <summary>
        /// Registers a factory method for decorating <typeparamref name="TService"/>
        /// </summary>
        //public void Decorate(Func<TService, IServiceProvider, TService> serviceFactory) {
        //    Registrar.Decorate(serviceFactory);
        //}

        public void Decorate(Func<TService, IServiceProvider, TService> serviceFactory) {
            Registrar.Decorate(serviceFactory);
        }
    }
}