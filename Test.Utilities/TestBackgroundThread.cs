using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SubSpec;
using Utilities;
using Xunit;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;

namespace Test.Utilities
{
    public class TestBackgroundThread
    {
        [Specification]
        public void WaitForAllWork_should_return_immediately_if_no_work_queued()
        {
            var stopWatch = new Stopwatch();
            "Given no work going on".Context(() =>
            {
                Assert.False(ParallelWork.IsWorkOrTimerQueued());
            });

            var result = default(bool);
            "When WaitForAllWork is called".Do(() =>
            {
                stopWatch.Start();
                result = ParallelWork.WaitForAllWork(TimeSpan.FromSeconds(1));
            });

            "It should return immediately without going into any wait period".Assert(() =>
            {
                Assert.True(stopWatch.Elapsed < TimeSpan.FromSeconds(1));
            });

            "It should return true".Assert(() =>
            {
                Assert.True(result);
            });
        }

        [Specification][STAThread]
        public void DoWork_should_queue_a_new_thread_to_do_the_work()
        {
            TimeSpan howLongWorkTakes = TimeSpan.FromSeconds(1);
            
            var doWorkCalled = false;
            var successCallbackFired = false;
            var onExceptionFired = false;

            var doWorkThreadId = default(int);
            var onCompleteThreadId = default(int);
            var onExceptionThreadId = default(int);
            var letsThrowException = false;

            Stopwatch stopWatch = new Stopwatch();
            DispatcherFrame frame = default(DispatcherFrame);
                    
            Func<bool> waitForWorkDone = () => {
                TimeSpan timeout = howLongWorkTakes.Add(TimeSpan.FromSeconds(5));
                if (ParallelWork.WaitForAllWork(timeout))
                {
                    // Let the Disptacher.BeginInvoke calls proceed
                    Dispatcher.PushFrame(frame);
                    return true;
                }
                else
                {
                    // waiting timed out. Work did not finish on time.
                    return false;
                }
            };

            Action callbackFiredOnDispatcher = () => {
                frame.Continue = false; // Dispatcher should stop now
            };

            "Given no background work running".Context(() =>
            {
                Assert.False(ParallelWork.IsWorkOrTimerQueued());                
                frame = new DispatcherFrame();

                doWorkCalled = false;
                successCallbackFired = false;
                onExceptionFired = false;

                doWorkThreadId = default(int);
                onCompleteThreadId = default(int);
                onExceptionThreadId = default(int);
                
                stopWatch.Reset();
                stopWatch.Start();
            });

            "When a new work is queued".Do(() =>
            {
                var shouldThrowException = letsThrowException;
                ParallelWork.DoWork(() => // doWork
                {
                    doWorkThreadId = Thread.CurrentThread.ManagedThreadId;
                    doWorkCalled = true;

                    // Simulate some delay in background work
                    Thread.Sleep(howLongWorkTakes); 

                    if (shouldThrowException)
                    {
                        throw new ApplicationException("Exception");
                    }                                
                }, () => // onComplete
                {
                    onCompleteThreadId = Thread.CurrentThread.ManagedThreadId;
                    successCallbackFired = true;

                    callbackFiredOnDispatcher();
                }, (x) => // onException
                {
                    onExceptionThreadId = Thread.CurrentThread.ManagedThreadId;
                    onExceptionFired = true;

                    callbackFiredOnDispatcher();
                });
            });

            "It should return control immediately without blocking the current thread".Assert(() =>
            {
                Assert.True(stopWatch.Elapsed < howLongWorkTakes);
                Assert.True(waitForWorkDone());
            });

            "It should return true if IsWorkQueued is called".Assert(() =>
            {
                Assert.True(ParallelWork.IsWorkOrTimerQueued());
                Assert.True(waitForWorkDone());
            });

            "It should wait for the work to complete if WaitForAllWork is called".Assert(() =>
            {
                Assert.True(waitForWorkDone());

                // The work should finish within the duration it takes with max 1 sec buffer
                // for additional stuff xunit does.
                Assert.True(stopWatch.Elapsed < howLongWorkTakes.Add(TimeSpan.FromSeconds(1)));
            });

            "It should execute the work in a separate thread".Assert(() => 
            {
                Assert.True(waitForWorkDone());

                Assert.True(doWorkCalled);
                Assert.NotEqual(Thread.CurrentThread.ManagedThreadId, doWorkThreadId);
            });

            "It should fire onComplete on the same thread as UI thread".Assert(() =>
            {
                Assert.True(waitForWorkDone());

                Assert.True(successCallbackFired);
                Assert.Equal(Thread.CurrentThread.ManagedThreadId, onCompleteThreadId);
            });
            
            "It should not fire onException if there's no exception".Assert(() =>
            {
                Assert.True(waitForWorkDone());

                Assert.False(onExceptionFired);

                letsThrowException = true; // This is for next assert                
            });

            "It should fire exception on UI thread".Assert(() =>
            {
                Assert.True(waitForWorkDone());

                Assert.False(successCallbackFired);
                Assert.True(onExceptionFired);
                Assert.Equal(Thread.CurrentThread.ManagedThreadId, onExceptionThreadId);
            });
        }

        
    }
}
