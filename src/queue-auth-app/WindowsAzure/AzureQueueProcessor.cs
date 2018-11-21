using Microsoft.WindowsAzure.Storage.Queue;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QueueAuth.WindowsAzure
{
    public interface IQueueProcessor
    {
        Task ExecuteAsync( CloudQueueMessage message, CancellationToken cancellationToken );
    }
}
