using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System;
using System.Collections.Generic;
using System.Text;

namespace QueueAuth.WindowsAzure
{
    internal class AzureQueueResolver
    {
        private readonly CloudQueueClient queueClient;

        public AzureQueueResolver( IOptions<AzureQueueResolverOptions> optionsAccessor )
        {
            var storageAccount = CloudStorageAccount.Parse( optionsAccessor.Value.ConnectionString );

            queueClient = storageAccount.CreateCloudQueueClient();
        }

        public CloudQueue GetQueue( string queueName )
        {
            return ( queueClient.GetQueueReference( queueName ) );
        }
    }
}
