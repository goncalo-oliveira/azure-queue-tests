using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Queue;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QueueAuth
{
    internal class QueueProcessor
    {
        private readonly ILogger log;
        private readonly CloudQueue queue;

        public QueueProcessor( ILoggerFactory loggerFactory, WindowsAzure.AzureQueueResolver queueResolver , IOptions<TestQueueOptions> optionsAccessor )
        {
            log = loggerFactory.CreateLogger( "queueProcessor" );
            queue = queueResolver.GetQueue( optionsAccessor.Value.QueueName );
        }

        public async Task ExecuteAsync( CloudQueueMessage message, CancellationToken cancellationToken )
        {
            log.LogDebug( "re-inserting message." );

            // add it back into the queue with a 10m visibility delay
            var newMessage = new CloudQueueMessage( message.AsString );

            await queue.AddMessageAsync( newMessage, null, TimeSpan.FromMinutes( 10 ), null, null, cancellationToken );
        }
    }
}
