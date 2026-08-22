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


namespace Console
{
    public class TermPTYProxy : ITerminalConnection, ITermPTYCallback, IDisposable
    {
        private static Process _serverProcess;
        private DuplexChannelFactory<ITermPTYService> _factory;
        private ITermPTYService _service;
        private static readonly object _lock = new object();
        private readonly object _serviceLock = new object();
        private bool _disposed;

        private readonly Guid _instanceId;
        public event EventHandler<TerminalOutputEventArgs> TerminalOutput;
        public event EventHandler TermReady;
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
                        new EndpointAddress("net.pipe://localhost/TermPTYService"));

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

        public void StartCmd(string command, int consoleWidth = 80, int consoleHeight = 30)
        {
            CallService(service => service.StartCmd(_instanceId, command, consoleWidth, consoleHeight));
            TermProcIsStarted = true;
        }

        void ITerminalConnection.Start()
        {
            //_service.Start(_instanceId);
        }
        void ITerminalConnection.WriteInput(string data)
        {
            CallService(service => service.WriteInput(_instanceId, data));
        }

        public void Resize(int height, int width)
        {
            CallService(service => service.Resize(_instanceId, width, height));
        }

        public void WriteToUITerminal(string str)
        {
            if (TerminalOutput != null)
                TerminalOutput(this, new TerminalOutputEventArgs(str));
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
            {
                TerminalOutput?.Invoke(this, new TerminalOutputEventArgs(output));
            }
        }

        void ITermPTYCallback.OnTermReady(Guid instanceId)
        {
            if (instanceId == _instanceId)
            {
                TermReady?.Invoke(this, EventArgs.Empty);
            }
        }

        private void CallService(Action<ITermPTYService> action)
        {
            lock (_serviceLock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(TermPTYProxy));
                action(_service);
            }
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
