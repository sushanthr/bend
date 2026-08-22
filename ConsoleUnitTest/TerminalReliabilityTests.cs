using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Console;
using Microsoft.Terminal.Wpf;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConsoleUnitTest
{
    [TestClass]
    public class TerminalReliabilityTests
    {
        [TestMethod]
        public void InputResizeAndCloseAreSafeBeforeProcessStarts()
        {
            var terminal = new TermPTY();

            terminal.WriteToTerm("ignored");
            terminal.WriteToTermBinary(new byte[] { 1, 2, 3 });
            terminal.Resize(80, 25);
            terminal.CloseStdinToApp();
            ((ITerminalConnection)terminal).Close();
        }

        [TestMethod]
        public void DirectTerminalOutputIsForwardedWithoutAddingNewlinesOrInput()
        {
            var terminal = new TermPTY();
            string output = null;
            terminal.TerminalOutput += delegate(object sender, TerminalOutputEventArgs args)
            {
                output = args.Data;
            };

            terminal.WriteToUITerminal("C:\\Users\\test>");

            Assert.AreEqual("C:\\Users\\test>", output);
        }

        [TestMethod]
        public void ProxyBuffersEarlyOutputAndFlushesItExactlyOnceOnStart()
        {
            TermPTYProxy proxy = CreateUnconnectedProxy();
            var received = new List<string>();
            proxy.WriteToUITerminal("C:\\Users\\test>");
            proxy.TerminalOutput += delegate(object sender, TerminalOutputEventArgs args) { received.Add(args.Data); };

            ((ITerminalConnection)proxy).Start();

            CollectionAssert.AreEqual(new[] { "C:\\Users\\test>" }, received);
        }

        [TestMethod]
        public void ProxyMarshalsWorkerOutputToConsumerSynchronizationContext()
        {
            TermPTYProxy proxy = CreateUnconnectedProxy();
            var context = new QueuedSynchronizationContext();
            var received = new List<string>();
            proxy.TerminalOutput += delegate(object sender, TerminalOutputEventArgs args) { received.Add(args.Data); };

            SynchronizationContext previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(context);
            try { ((ITerminalConnection)proxy).Start(); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }

            Task.Run(() => proxy.WriteToUITerminal("prompt")).Wait();
            Assert.AreEqual(0, received.Count, "Output ran directly on the worker thread.");
            Assert.AreEqual(1, context.PendingCount);

            context.Drain();
            CollectionAssert.AreEqual(new[] { "prompt" }, received);
        }

        private static TermPTYProxy CreateUnconnectedProxy()
        {
            var proxy = (TermPTYProxy)FormatterServices.GetUninitializedObject(typeof(TermPTYProxy));
            SetField(proxy, "_outputLock", new object());
            SetField(proxy, "_serviceLock", new object());
            SetField(proxy, "_pendingOutput", new Queue<string>());
            return proxy;
        }

        private static void SetField(object target, string name, object value)
        {
            typeof(TermPTYProxy).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private sealed class QueuedSynchronizationContext : SynchronizationContext
        {
            private readonly Queue<Tuple<SendOrPostCallback, object>> _work = new Queue<Tuple<SendOrPostCallback, object>>();
            public int PendingCount { get { lock (_work) return _work.Count; } }
            public override void Post(SendOrPostCallback callback, object state)
            {
                lock (_work) _work.Enqueue(Tuple.Create(callback, state));
            }
            public void Drain()
            {
                while (true)
                {
                    Tuple<SendOrPostCallback, object> item;
                    lock (_work)
                    {
                        if (_work.Count == 0) return;
                        item = _work.Dequeue();
                    }
                    item.Item1(item.Item2);
                }
            }
        }
    }
}
