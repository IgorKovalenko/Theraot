﻿//
// TaskCompletionSourceTests.cs
//
// Author:
//       Jérémie "Garuma" Laval <jeremie.laval@gmail.com>
//
// Copyright (c) 2009 Jérémie "Garuma" Laval
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

#define NET_4_0
#if NET_4_0

using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MonoTests.System.Threading.Tasks
{
    [TestFixture]
    public class TaskCompletionSourceTests
    {
        private TaskCompletionSource<int> _completionSource;
        private object _state;

        [Test]
        [Category("RaceCondition")]
        public void ContinuationTest()
        {
            var result = false;
            var t = _completionSource.Task.ContinueWith((p) => result |= ((Task<int>)p).Result == 2);
            Assert.AreEqual(TaskStatus.WaitingForActivation, _completionSource.Task.Status, "#A");
            _completionSource.SetResult(2);
            t.Wait();
            Assert.AreEqual(TaskStatus.RanToCompletion, _completionSource.Task.Status, "#1");
            Assert.AreEqual(TaskStatus.RanToCompletion, t.Status, "#2");
            Assert.IsTrue(result);
        }

        [Test]
        public void CreationCheckTest()
        {
            Assert.IsNotNull(_completionSource.Task, "#1");
            Assert.AreEqual(TaskCreationOptions.None, _completionSource.Task.CreationOptions, "#2");
        }

        [Test]
        public void CtorInvalidOptions()
        {
            try
            {
                var taskCompletionSource = new TaskCompletionSource<long>(TaskCreationOptions.LongRunning);
                GC.KeepAlive(taskCompletionSource);
                Assert.Fail("#1");
            }
            catch (ArgumentOutOfRangeException ex)
            {
                GC.KeepAlive(ex);
            }

            try
            {
                var taskCompletionSource = new TaskCompletionSource<long>(TaskCreationOptions.PreferFairness);
                GC.KeepAlive(taskCompletionSource);
                Assert.Fail("#2");
            }
            catch (ArgumentOutOfRangeException ex)
            {
                GC.KeepAlive(ex);
            }
        }

        [Test]
        public void FaultedFutureTest()
        {
            var thrown = new ApplicationException();
            var source = new TaskCompletionSource<int>();
            source.TrySetException(thrown);
            var f = source.Task;
            AggregateException ex = null;
            try
            {
                f.Wait();
            }
            catch (AggregateException e)
            {
                ex = e;
            }

            Assert.IsNotNull(ex);
            Assert.AreEqual(thrown, ex.InnerException);
            Assert.AreEqual(thrown, f.Exception.InnerException);
            Assert.AreEqual(TaskStatus.Faulted, f.Status);

            ex = null;
            try
            {
                var result = f.Result;
            }
            catch (AggregateException e)
            {
                ex = e;
            }

            Assert.IsNotNull(ex);
            Assert.AreEqual(TaskStatus.Faulted, f.Status);
            Assert.AreEqual(thrown, f.Exception.InnerException);
            Assert.AreEqual(thrown, ex.InnerException);
        }

        [Test]
        public void SetCanceledTest()
        {
            Assert.IsNotNull(_completionSource.Task, "#1");
            Assert.IsTrue(_completionSource.TrySetCanceled(), "#2");
            Assert.AreEqual(TaskStatus.Canceled, _completionSource.Task.Status, "#3");
            Assert.IsFalse(_completionSource.TrySetResult(42), "#4");
            Assert.AreEqual(TaskStatus.Canceled, _completionSource.Task.Status, "#5");

            try
            {
                Console.WriteLine(_completionSource.Task.Result);
                Assert.Fail("#6");
            }
            catch (AggregateException e)
            {
                var details = (TaskCanceledException)e.InnerException;
                Assert.AreEqual(_completionSource.Task, details.Task, "#6e");
                Assert.IsNull(details.Task.Exception, "#6e2");
            }
        }

        [Test]
        [Ignore("#4550, Mono GC is lame")]
        public void SetExceptionAndUnobservedEvent()
        {
            var notFromMainThread = false;
            using (var mre = new ManualResetEvent(false))
            {
                var mainThreadId = Thread.CurrentThread.ManagedThreadId;
                TaskScheduler.UnobservedTaskException += (o, args) =>
                {
                    notFromMainThread = Thread.CurrentThread.ManagedThreadId != mainThreadId;
                    args.SetObserved();
                    mre.Set();
                };
                var inner = new ApplicationException();
                CreateFaultedTaskCompletionSource(inner);
                GC.Collect();
                GC.WaitForPendingFinalizers();

                Assert.IsTrue(mre.WaitOne(5000), "#1");
                Assert.IsTrue(notFromMainThread, "#2");
            }
        }

        [Test]
        public void SetExceptionInvalid()
        {
            try
            {
                _completionSource.TrySetException(new ApplicationException[0]);
                Assert.Fail("#1");
            }
            catch (ArgumentException ex)
            {
                GC.KeepAlive(ex);
            }

            try
            {
                _completionSource.TrySetException(new[] { new ApplicationException(), null });
                Assert.Fail("#2");
            }
            catch (ArgumentException ex)
            {
                GC.KeepAlive(ex);
            }

            Assert.AreEqual(TaskStatus.WaitingForActivation, _completionSource.Task.Status, "r1");
        }

        [Test]
        public void SetExceptionTest()
        {
            var e = new Exception("foo");

            Assert.IsNotNull(_completionSource.Task, "#1");
            Assert.IsTrue(_completionSource.TrySetException(e), "#2");
            Assert.AreEqual(TaskStatus.Faulted, _completionSource.Task.Status, "#3");
            Assert.That(_completionSource.Task.Exception, Is.TypeOf(typeof(AggregateException)), "#4.1");

            var aggr = (AggregateException)_completionSource.Task.Exception;
            Assert.AreEqual(1, aggr.InnerExceptions.Count, "#4.2");
            Assert.AreEqual(e, aggr.InnerExceptions[0], "#4.3");

            Assert.IsFalse(_completionSource.TrySetResult(42), "#5");
            Assert.AreEqual(TaskStatus.Faulted, _completionSource.Task.Status, "#6");
            Assert.IsFalse(_completionSource.TrySetCanceled(), "#8");
            Assert.AreEqual(TaskStatus.Faulted, _completionSource.Task.Status, "#9");
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void SetResultExceptionTest()
        {
            Assert.IsNotNull(_completionSource.Task, "#1");
            Assert.IsTrue(_completionSource.TrySetResult(42), "#2");
            Assert.AreEqual(TaskStatus.RanToCompletion, _completionSource.Task.Status, "#3");
            Assert.AreEqual(42, _completionSource.Task.Result, "#4");

            _completionSource.SetResult(43);
        }

        [Test]
        public void SetResultTest()
        {
            Assert.IsNotNull(_completionSource.Task, "#1");
            Assert.IsTrue(_completionSource.TrySetResult(42), "#2");
            Assert.AreEqual(TaskStatus.RanToCompletion, _completionSource.Task.Status, "#3");
            Assert.AreEqual(42, _completionSource.Task.Result, "#4");
            Assert.IsFalse(_completionSource.TrySetResult(43), "#5");
            Assert.AreEqual(TaskStatus.RanToCompletion, _completionSource.Task.Status, "#6");
            Assert.AreEqual(42, _completionSource.Task.Result, "#7");
            Assert.IsFalse(_completionSource.TrySetCanceled(), "#8");
            Assert.AreEqual(TaskStatus.RanToCompletion, _completionSource.Task.Status, "#9");
        }

        [SetUp]
        public void Setup()
        {
            _state = new object();
            _completionSource = new TaskCompletionSource<int>(_state, TaskCreationOptions.None);
        }

        [Test]
        public void WaitingTest()
        {
            var tcs = new TaskCompletionSource<int>();
            var task = tcs.Task;
            var result = task.Wait(50);

            Assert.IsFalse(result);
        }

        private static void CreateFaultedTaskCompletionSource(Exception inner)
        {
            var tcs = new TaskCompletionSource<int>();
            tcs.SetException(inner);
            tcs = null;
        }
    }
}

#endif