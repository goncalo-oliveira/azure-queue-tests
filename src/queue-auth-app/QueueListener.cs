using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Queue;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QueueAuth
{
    internal class QueueListener : IDisposable
    {
        private readonly CloudQueue queue;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly object stopWaitingTaskSourceLock = new object();
        private readonly QueueProcessor processor;
        private readonly ILogger log;
        private readonly TimeSpan backoffDelay = TimeSpan.FromSeconds( 60 );
        private readonly TimeSpan visibilityTimeout = TimeSpan.FromMinutes( 5 );

        private Task run;
        private bool hasStarted;
        private bool isStopped;
        private int iterationsWithoutMessages = 0;

        private bool isDisposed;
        private TaskCompletionSource<object> stopWaitingTaskSource;

        public QueueListener( CloudQueue queue, QueueProcessor queueProcessor, ILoggerFactory loggerFactory )
        {
            this.queue = queue;
            processor = queueProcessor;

            log = loggerFactory?.CreateLogger( "queue-listener" );
        }

        public void Dispose()
        {
            if ( !isDisposed )
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();

                isDisposed = true;
            }
        }

        public async Task StartAsync()
        {
            ThrowIfDisposed();

            if ( hasStarted )
            {
                throw new InvalidOperationException();
            }

            await queue.CreateIfNotExistsAsync();

            run = RunAsync( cancellationTokenSource.Token );
            hasStarted = true;

            log?.LogInformation( "started." );
        }

        public Task StopAsync( CancellationToken cancellationToken )
        {
            ThrowIfDisposed();

            if ( !hasStarted )
            {
                throw new InvalidOperationException();
            }

            if ( isStopped )
            {
                throw new InvalidOperationException();
            }

            log?.LogInformation( "stopping..." );

            cancellationTokenSource.Cancel();

            return ( InternalStopAsync( cancellationToken ) );
        }

        private async Task InternalStopAsync( CancellationToken cancellationToken )
        {
            TaskCompletionSource<object> cancellationTaskSource = new TaskCompletionSource<object>();

            using ( cancellationToken.Register( () => cancellationTaskSource.SetCanceled() ) )
            {
                await Task.WhenAny( run, cancellationTaskSource.Task );
            }

            isStopped = true;

            log?.LogInformation( "stopped." );
        }

        public void Cancel()
        {
            ThrowIfDisposed();
            cancellationTokenSource.Cancel();
        }

        private async Task RunAsync( CancellationToken cancellationToken )
        {
            try
            {
                await Task.Yield();

                Task wait = Task.Delay( 0 );

                // Execute tasks one at a time (in a series) until stopped.
                while ( !cancellationToken.IsCancellationRequested )
                {
                    TaskCompletionSource<object> cancellationTaskSource = new TaskCompletionSource<object>();

                    using ( cancellationToken.Register( () => cancellationTaskSource.SetCanceled() ) )
                    {
                        try
                        {
                            await Task.WhenAny( wait, cancellationTaskSource.Task );
                        }
                        catch ( OperationCanceledException )
                        {
                            // When Stop fires, don't make it wait for wait before it can return.
                        }
                    }

                    if ( cancellationToken.IsCancellationRequested )
                    {
                        break;
                    }

                    try
                    {
                        wait = await ExecuteAsync( cancellationToken );
                    }
                    catch ( OperationCanceledException )
                    {
                        // Don't fail the task, throw a background exception, or stop looping when a task cancels.
                    }
                }
            }
            catch ( Exception ex )
            {
                log?.LogError( ex, "error running listener." );
                throw;
            }
        }

        private async Task<Task> ExecuteAsync( CancellationToken cancellationToken )
        {
            lock ( stopWaitingTaskSourceLock )
            {
                if ( stopWaitingTaskSource != null )
                {
                    stopWaitingTaskSource.TrySetResult( null );
                }

                stopWaitingTaskSource = new TaskCompletionSource<object>();
            }

            CloudQueueMessage message = null;

            try
            {
                message = await queue.GetMessageAsync();
            }
            catch ( Exception ex )
            {
                if ( ex.InnerException?.GetType() == typeof( TaskCanceledException ) )
                {
                    // do nothing
                }
                else
                {
                    log?.LogError( ex, "failed to retrieve message from queue." );

                    throw;
                }
            }

            if ( message == null )
            {
                return ( CreateBackoffTask() );
            }

            log?.LogDebug( "new message. id: {0}  visibilityTimeout: {1:F2}s", message.Id, visibilityTimeout.TotalSeconds );

            return ( ProcessMessageAsync( message, cancellationToken ) );
        }

        private async Task ProcessMessageAsync( CloudQueueMessage message, CancellationToken cancellationToken )
        {
            iterationsWithoutMessages = 0;

            try
            {
                // do stuff
                await processor.ExecuteAsync( message, cancellationToken );

                await queue.DeleteMessageAsync( message );

                log?.LogDebug( "deleted message from queue. id: {0}", message.Id );
            }
            catch ( Exception ex )
            {
                log?.LogError( ex, "failed to process message." );
            }
        }

        private Task CreateBackoffTask()
        {
            TimeSpan progressiveDelay = backoffDelay;

            if ( iterationsWithoutMessages > 0 )
            {
                progressiveDelay = backoffDelay.Add( TimeSpan.FromSeconds( 10 * iterationsWithoutMessages ) );
            }

            log?.LogDebug( "message not found. next request delayed for {0:F0} seconds.", progressiveDelay.TotalSeconds );

            Task delayTask = Task.Delay( progressiveDelay );

            if ( iterationsWithoutMessages < 2 )
            {
                iterationsWithoutMessages++;
            }

            return ( Task.WhenAny( stopWaitingTaskSource.Task, delayTask ) );
        }

        private void ThrowIfDisposed()
        {
            if ( isDisposed )
            {
                throw new ObjectDisposedException( null );
            }
        }
    }
}
