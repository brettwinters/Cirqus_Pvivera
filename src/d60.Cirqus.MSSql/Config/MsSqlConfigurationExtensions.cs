using System;
using d60.Cirqus.Config;
using d60.Cirqus.Config.Configurers;
using d60.Cirqus.Events;
using d60.Cirqus.MsSql.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace d60.Cirqus.MsSql.Config
{
    public static class MsSqlConfigurationExtensions
    {
        public static void UseSqlServer(this EventStoreConfigurationBuilder builder, string connectionStringName, string tableName, bool automaticallyCreateSchema = true)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (connectionStringName == null) throw new ArgumentNullException(nameof(connectionStringName));
            if (tableName == null) throw new ArgumentNullException(nameof(tableName));

            builder.Register<IEventStore>(context => new MsSqlEventStore(context.GetService<IConfigurationRoot>(),
                connectionStringName, 
                tableName,
                automaticallyCreateSchema: automaticallyCreateSchema));
        }
    }
}