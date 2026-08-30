using Microsoft.Terminal.Wpf;

using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using System.Collections.Generic;


namespace Console
{
    [CallbackBehavior(UseSynchronizationContext = false, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class TermPTYProxy : ITerminalConnection, ITermPTYCallback, IDisposable
    {
        private static Process _serverProcess;
        private DuplexChannelFactory<ITermPTYService> _factory;
        private ITermPTYService _service;
        private static readonly object _lock = new object();
        private readonly object _serviceLock = new object();
        private readonly object _outputLock = new object();
        private readonly Queue<string> _pendingOutput = new Queue<string>();
        private EventHandler<TerminalOutputEventArgs> _terminalOutput;
        private SynchronizationContext _consumerContext;
        private bool _consumerStarted;
        private bool _disposed;
        private static readonly string ServiceAddress = "net.pipe://localhost/Bend/TermPTYService/" + Process.GetCurrentProcess().Id;

        private readonly Guid _instanceId;
        public event EventHandler<TerminalOutputEventArgs> TerminalOutput
        {
            add
            {
                lock (_outputLock)
                {
                    _terminalOutput += value;
                    if (_consumerStarted)
                        FlushPendingOutput(value);
                }
            }
            remove
            {
                lock (_outputLock)
                    _terminalOutput -= value;
            }
        }
        public event EventHandler TermReady;
        public event EventHandler TermExited;
        public bool TermProcIsStarted { get; private set; }

        public TermPTYProxy()
        {
            EnsureServerRunning();

            const int maxConnectAttempts = 10;
            Exception lastError = null;
            int attempt = 0;
            do
            {
                try
                {
                    var binding = new NetNamedPipeBinding() { MaxReceivedMessageSize = 1024 * 1024 };
                    _factory = new DuplexChannelFactory<ITermPTYService>(
                        new InstanceContext(this),
                        binding,
                        new EndpointAddress(ServiceAddress));

                    _service = _factory.CreateChannel();
                    _instanceId = _service.CreateInstance();
                    return;
                }
                catch (EndpointNotFoundException ex)
                {
                    lastError = ex;
                    AbortFactory();
                    Thread.Sleep(500);
                }
                catch (CommunicationException ex)
                {
                    lastError = ex;
                    AbortFactory();
                    Thread.Sleep(500);
                }
                catch (TimeoutException ex)
                {
                    lastError = ex;
                    AbortFactory();
                    Thread.Sleep(500);
                }
            } while (++attempt < maxConnectAttempts);

            throw new InvalidOperationException("The Bend console host did not become available.", lastError);
        }

        public static void EnsureServerRunning()
        {
            lock (_lock)
            {
                if (_serverProcess == null || _serverProcess.HasExited)
                {
                    var serverPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BendConsoleHost.exe");
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = serverPath,
                        Arguments = Process.GetCurrentProcess().Id.ToString(),
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    if (!File.Exists(serverPath))
                        throw new FileNotFoundException("The Bend console host executable was not found.", serverPath);

                    _serverProcess = Process.Start(startInfo);
                    if (_serverProcess == null)
                        throw new InvalidOperationException("The Bend console host process could not be started.");
                }
            }
        }

        public void StartCmd(string command, int consoleWidth = 80, int consoleHeight = 30, string workingDirectory = null)
        {
            TermProcIsStarted = TryCallService(service => service.StartCmd(_instanceId, command, consoleWidth, consoleHeight, workingDirectory));
        }

        public void WriteInput(string data)
        {
            TryCallService(service => service.WriteInput(_instanceId, data));
        }

        void ITerminalConnection.Start()
        {
            lock (_outputLock)
            {
                _consumerContext = SynchronizationContext.Current;
                _consumerStarted = true;
                if (_terminalOutput != null)
                    FlushPendingOutput(_terminalOutput);
            }
        }
        void ITerminalConnection.WriteInput(string data)
        {
            WriteInput(data);
        }

        public void Resize(int height, int width)
        {
            TryCallService(service => service.Resize(_instanceId, width, height));
        }

        public void WriteToUITerminal(string str)
        {
            DispatchTerminalOutput(str);
        }

        public void SetReadOnly(bool readOnly = true, bool updateCursor = true)
        {
        }

        public void SetCursorVisibility(bool visible)
        {
            WriteToUITerminal("\x1b[?25" + (visible ? "h" : "l"));
        }

        public void Win32DirectInputMode(bool enable)
        {
            WriteToUITerminal("\x1b[?9001" + (enable ? "h" : "l"));
        }

        void ITerminalConnection.Resize(uint height, uint width)
        {
            Resize((int)height, (int)width);
        }

        void ITerminalConnection.Close()
        {
            Dispose();
        }

        void ITermPTYCallback.OnTerminalOutput(Guid instanceId, string output)
        {
            if (instanceId == _instanceId)
                DispatchTerminalOutput(output);
        }

        void ITermPTYCallback.OnTermReady(Guid instanceId)
        {
            if (instanceId == _instanceId)
            {
                TermReady?.Invoke(this, EventArgs.Empty);
            }
        }

        private bool TryCallService(Action<ITermPTYService> action)
        {
            lock (_serviceLock)
            {
                if (_disposed || _service == null)
                    return false;
                try
                {
                    action(_service);
                    return true;
                }
                catch (CommunicationException)
                {
                    AbortFactory();
                    return false;
                }
                catch (TimeoutException)
                {
                    AbortFactory();
                    return false;
                }
                catch (ObjectDisposedException)
                {
                    AbortFactory();
                    return false;
                }
            }
        }

        void ITermPTYCallback.OnTermExited(Guid instanceId)
        {
            if (instanceId != _instanceId) return;
            TermProcIsStarted = false;
            EventHandler handler = TermExited;
            if (handler == null) return;
            SynchronizationContext consumerContext = _consumerContext;
            if (consumerContext != null && SynchronizationContext.Current != consumerContext)
                consumerContext.Post(_ => handler(this, EventArgs.Empty), null);
            else
                handler(this, EventArgs.Empty);
        }

        private void DispatchTerminalOutput(string output)
        {
            EventHandler<TerminalOutputEventArgs> handler;
            SynchronizationContext consumerContext;
            lock (_outputLock)
            {
                handler = _terminalOutput;
                if (handler == null || !_consumerStarted)
                {
                    _pendingOutput.Enqueue(output);
                    return;
                }
                consumerContext = _consumerContext;
            }

            if (consumerContext != null && SynchronizationContext.Current != consumerContext)
            {
                consumerContext.Post(_ => handler(this, new TerminalOutputEventArgs(output)), null);
            }
            else
            {
                handler(this, new TerminalOutputEventArgs(output));
            }
        }

        private void FlushPendingOutput(EventHandler<TerminalOutputEventArgs> handler)
        {
            while (_pendingOutput.Count > 0)
                handler(this, new TerminalOutputEventArgs(_pendingOutput.Dequeue()));
        }

        private void AbortFactory()
        {
            var channel = _service as ICommunicationObject;
            channel?.Abort();
            _factory?.Abort();
            _service = null;
            _factory = null;
        }

        public void Dispose()
        {
            lock (_serviceLock)
            {
                if (_disposed)
                    return;
                _disposed = true;

                try
                {
                    _service?.Close(_instanceId);
                    (_service as ICommunicationObject)?.Close();
                    _factory?.Close();
                }
                catch (CommunicationException)
                {
                    AbortFactory();
                }
                catch (TimeoutException)
                {
                    AbortFactory();
                }
            }
        }
    }
}
