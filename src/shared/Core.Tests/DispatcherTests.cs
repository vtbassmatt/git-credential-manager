using System;
using System.Threading;
using System.Threading.Tasks;
using GitCredentialManager.UI;
using Xunit;

namespace GitCredentialManager.Tests
{
    public class DispatcherTests
    {
        [Fact]
        public async Task Dispatcher_InvokeAsync_AsyncFunc_ExecutesOnDispatcherThread()
        {
            int dispatcherThreadId = -1;
            int workThreadId = -1;

            var dispatcher = StartDispatcher(out var thread, out var shutdown);

            try
            {
                dispatcherThreadId = thread.ManagedThreadId;

                string result = await dispatcher.InvokeAsync(async () =>
                {
                    workThreadId = Thread.CurrentThread.ManagedThreadId;
                    await Task.Yield();
                    return "hello";
                });

                Assert.Equal("hello", result);
                Assert.Equal(dispatcherThreadId, workThreadId);
            }
            finally
            {
                dispatcher.Shutdown();
                shutdown.Wait(TimeSpan.FromSeconds(5));
            }
        }

        [Fact]
        public async Task Dispatcher_InvokeAsync_AsyncFunc_PropagatesException()
        {
            var dispatcher = StartDispatcher(out _, out var shutdown);

            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    dispatcher.InvokeAsync<string>(async () =>
                    {
                        await Task.Yield();
                        throw new InvalidOperationException("test error");
                    }));
            }
            finally
            {
                dispatcher.Shutdown();
                shutdown.Wait(TimeSpan.FromSeconds(5));
            }
        }

        /// <summary>
        /// Start a new Dispatcher on a dedicated thread and return it.
        /// </summary>
        private static Dispatcher StartDispatcher(out Thread thread, out ManualResetEventSlim shutdownEvent)
        {
            Dispatcher dispatcher = null;
            var ready = new ManualResetEventSlim();
            var done = new ManualResetEventSlim();

            var t = new Thread(() =>
            {
                Dispatcher.Initialize();
                dispatcher = Dispatcher.MainThread;
                ready.Set();
                dispatcher.Run();
                done.Set();
            });
            t.Start();
            ready.Wait(TimeSpan.FromSeconds(5));

            thread = t;
            shutdownEvent = done;
            return dispatcher;
        }
    }
}
