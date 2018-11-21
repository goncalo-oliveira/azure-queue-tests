using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QueueAuth
{
    public class ConsoleApplication : IDisposable
    {
        private static readonly Lazy<bool> isInteractive = new Lazy<bool>( TryIsInteractive );

        private readonly CancellationTokenSource shutdownTokenSource = new CancellationTokenSource();
        private readonly CancellationTokenSource stoppingTokenSource = new CancellationTokenSource();

        private bool isDisposed;

        private static bool TryIsInteractive()
        {
            try
            {
                bool keyAvailable = Console.KeyAvailable;
            }
            catch ( InvalidOperationException )
            {
                return ( false );
            }

            return ( true );
        }

        private ConsoleApplication()
        { }

        public static bool IsInteractive
        {
            get
            {
                return ( isInteractive.Value );
            }
        }

        public static void Wait()
        {
            ConsoleApplication host = new ConsoleApplication();

            Console.CancelKeyPress += host.Console_CancelKeyPress;

            host.BlockCurrentThread();
        }

        /// <summary>
        /// Blocks and waits for any key press if the application is being debugged
        /// </summary>
        public static void DebuggerWaitForKey()
        {
            if ( Debugger.IsAttached )
            {
                Console.WriteLine( "press any key..." );
                Console.ReadKey( true );
            }
        }

        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        [SuppressMessage( "Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_shutdownTokenSource" )]
        protected virtual void Dispose( bool isDisposing )
        {
            if ( isDisposing && !isDisposed )
            {
                shutdownTokenSource.Cancel();

                stoppingTokenSource.Dispose();

                isDisposed = true;
            }
        }

        private void BlockCurrentThread()
        {
            if ( Debugger.IsAttached && IsInteractive )
            {
                Console.WriteLine( "Debugger is attached." );

                // Wait for ESC key
                while ( true )
                {
                    Task.Delay( 1000 )
                        .GetAwaiter()
                        .GetResult();

                    if ( Console.KeyAvailable && ( System.Console.ReadKey( true ).Key == ConsoleKey.Escape ) )
                    {
                        Console.WriteLine( "ESC pressed." );
                        break;
                    }
                }
            }
            else
            {
                // Unless debugger is attached, wait for someone to begin stopping (Ctrl+C, _shutdownWatcher, Stop, or Dispose).
                stoppingTokenSource.Token.WaitHandle.WaitOne();
            }
        }

        private void Console_CancelKeyPress( object sender, ConsoleCancelEventArgs e )
        {
            Console.WriteLine( "Ctrl+C pressed." );

            shutdownTokenSource.Cancel();

            e.Cancel = true;
        }
    }
}
