using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace QueueAuth
{
    class Program
    {
        static void Main( string[] args )
        {
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables( "QUEUE_AUTH_" )
#if DEBUG
                .SetBasePath( System.IO.Path.Combine( Environment.CurrentDirectory, "../../../" ) )
                .AddJsonFile( "config.json", optional: true, reloadOnChange: false )
#endif
                .Build();

            IServiceCollection serviceCollection = new ServiceCollection()
                .AddLogging( builder => builder.SetMinimumLevel( LogLevel.Debug ) )
                .AddTransient<WindowsAzure.AzureQueueResolver>()
                .Configure<WindowsAzure.AzureQueueResolverOptions>( options =>
                {
                    options.ConnectionString = configuration["AZURE_STORAGE_CONNECTIONSTRING"];
                } )
                .AddTransient<QueueProcessor>()
                .Configure<TestQueueOptions>( options =>
                {
                    options.QueueName = configuration["QUEUE_NAME"];
                } );

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>()
                .AddConsole( LogLevel.Debug, true );

            var queueResolver = serviceProvider.GetRequiredService<WindowsAzure.AzureQueueResolver>();
            var queueListener = new QueueListener( queueResolver.GetQueue( configuration["QUEUE_NAME"] )
                , serviceProvider.GetRequiredService<QueueProcessor>()
                , loggerFactory );

            queueListener.StartAsync()
                .GetAwaiter()
                .GetResult();

            ConsoleApplication.Wait();

            queueListener.StopAsync( CancellationToken.None )
                .GetAwaiter()
                .GetResult();
        }
    }
}
