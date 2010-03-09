using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading;
using System.Windows.Threading;
using System.Diagnostics;

namespace Utilities
{
    public static class BackgroundWork
    {
        private static readonly List<Thread> _threadPool = new List<Thread>();
        private static readonly List<DispatcherTimer> _timerPool = new List<DispatcherTimer>();

        private static ManualResetEvent _AllBackgroundThreadCompletedEvent = new ManualResetEvent(true);
        private static ManualResetEvent _AllTimerFiredEvent = new ManualResetEvent(true);

        public static void DoWork(Action doWork, Action onComplete)
        {
            DoWork(doWork, onComplete, (x) => { throw x; });
        }

        public static void DoWork(Action doWork, Action onComplete, Action<Exception> failed)
        {
            DoWork<object>(() => { doWork(); return true; }, (o) => onComplete(), failed);
        }

        public static void DoWork<T>(Func<T> doWork, Action<T> onComplete)
        {
            DoWork<T>(doWork, onComplete, (x) => { throw x; });
        }

        public static void DoWork<T>(Func<T> doWork, Action<T> onComplete, Action<Exception> fail)
        {
            DoWork<object, T>(new object(), 
                (o, progressCallback) => { return doWork(); },
                (o, msg, done) => { },
                (o, result) => onComplete(result),
                (o, x) => { fail(x); }
                );
        }

        public static void DoWork<T, R>(
            T arg,
            Func<T, Action<T, string, int>, R> doWork,
            Action<T, string, int> progress,
            Action<T, R> onComplete,
            Action<T, Exception> fail)
        {
            Dispatcher currentDispatcher = Dispatcher.CurrentDispatcher;
            Thread newThread = new Thread(new ParameterizedThreadStart( (thread)=>
                {
                    var currentThread = thread as Thread;
                    try
                    {
                        Debug.WriteLine(currentThread.ManagedThreadId + " Work execution stated: " + DateTime.Now.ToString());

                        R result = doWork(arg,
                            (data, message, percent) => currentDispatcher.BeginInvoke(progress, arg, message, percent));

                        if (null == result)
                        {
                            try
                            {
                                currentDispatcher.BeginInvoke(fail, arg, null);
                            }
                            catch
                            {
                                // Incase the error handler produces exception, we have to gracefully
                                // handle it since this is a background thread
                            }
                            finally
                            {
                                // Nothing to do, error handler is not supposed to produce more error
                            }
                        }
                        else
                        {
                            try
                            {
                                currentDispatcher.BeginInvoke(onComplete, arg, result);
                            }
                            catch (Exception x)
                            {
                                currentDispatcher.BeginInvoke(fail, arg, x);
                            }
                        }
                    }
                    catch (ThreadAbortException ex)
                    {
                        Debug.WriteLine(ex);
                    }
                    catch (Exception x)
                    {
                        currentDispatcher.BeginInvoke(fail, arg, x);
                    }
                    finally
                    {

                        Debug.WriteLine(currentThread.ManagedThreadId + " Work execution completed: " + DateTime.Now.ToString());

                        lock (_threadPool)
                        {
                            _threadPool.Remove(thread as Thread);
                            if (_threadPool.Count == 0)
                            {
                                _AllBackgroundThreadCompletedEvent.Set();
                                Debug.WriteLine("All Work completed: " + DateTime.Now.ToString());
                            }
                        }
                    }
                }));
            
            // Store the thread in a pool so that it is not garbage collected
            lock(_threadPool) 
                _threadPool.Add(newThread);

            _AllBackgroundThreadCompletedEvent.Reset();
            Debug.WriteLine(newThread.ManagedThreadId + " Work queued at: " + DateTime.Now.ToString());            

            newThread.SetApartmentState(ApartmentState.STA);
            newThread.Start(newThread);            
        }

        public static DispatcherTimer DoWorkAfter(
            Action onComplete,
            TimeSpan duration)
        {
            return DoWorkAfter(() => { }, (msg, done) => { }, onComplete, (x) => { throw x; }, duration);
        }

        public static DispatcherTimer DoWorkAfter(
            Action doWork,
            Action onComplete,
            TimeSpan duration)            
        {
            return DoWorkAfter(doWork, (msg, done) => { }, onComplete, (x) => { throw x; }, duration);
        }

        public static DispatcherTimer DoWorkAfter(
            Action doWork,
            Action onComplete,
            Action<Exception> onException,
            TimeSpan duration)
        {
            return DoWorkAfter(doWork, (msg, done) => { }, onComplete, onException, duration);
        }
        
        public static DispatcherTimer DoWorkAfter(
            Action doWork, 
            Action<string, int> onProgress,
            Action onComplete, 
            Action<Exception> onError, 
            TimeSpan duration)
        {
            var currentDispatcher = Dispatcher.CurrentDispatcher;
            return DoWorkAfter<Dispatcher, bool>(currentDispatcher,
                (dispatcher, progress) => { doWork(); return true; }, 
                (dispatcher, msg, done) => onProgress(msg, done), 
                (dispatcher, result) => onComplete(),
                (dispatcher, x) => onError(x), 
                duration);
        }

        public static DispatcherTimer DoWorkAfter<T, R>(
            T arg,
            Func<T, Action<T, string, int>, R> doWork, 
            Action<T, string, int> onProgress,
            Action<T, R> onComplete, 
            Action<T, Exception> onError, 
            TimeSpan duration)
        {
            var timer = new DispatcherTimer(duration, DispatcherPriority.Normal, new EventHandler((sender, e) =>
            {
                var currentTimer = (sender as DispatcherTimer);
                currentTimer.Stop();
                lock (_timerPool)
                {
                    _timerPool.Remove(currentTimer);
                    if (_timerPool.Count == 0)
                        _AllTimerFiredEvent.Set();
                }

                BackgroundWork.DoWork<T, R>(arg, doWork, onProgress, onComplete, onError);
            }),
            Dispatcher.CurrentDispatcher);

            lock(_timerPool)
                _timerPool.Add(timer);
            timer.Start();

            _AllTimerFiredEvent.Reset();
            return timer;
        }

        public static void StopAll()
        {
            while (_threadPool.Count > 0)
            {
                Thread t = _threadPool[0];
                try
                {
                    t.Abort();
                }
                finally
                {
                    lock (_threadPool)
                        if (_threadPool.Contains(t))
                            _threadPool.Remove(t);
                }
            }
        }

        public static bool IsWorkQueued()
        {
            lock (_timerPool)
                if (_timerPool.Count > 0)
                    return true;
            lock (_threadPool)
                if (_threadPool.Count > 0)
                    return true;

            return false;
        }

        public static bool WaitForAllWork(TimeSpan timeout)
        {
            lock (_threadPool)
                if (_threadPool.Count == 0)
                    return true;

            Debug.WriteLine("Start waiting: " + DateTime.Now.ToString());
            var result = _AllBackgroundThreadCompletedEvent.WaitOne(timeout);
            Debug.WriteLine("End waiting: " + DateTime.Now.ToString());
            return result;
        }
    }
}
