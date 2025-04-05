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


namespace Console
{
    public class TermPTYProxy : ITerminalConnection, ITermPTYCallback
    {
        private static Process _serverProcess;
        private static DuplexChannelFactory<ITermPTYService> _factory;
        private static ITermPTYService _service;
        private static readonly object _lock = new object();

        private readonly Guid _instanceId;
        public event EventHandler<TerminalOutputEventArgs> TerminalOutput;
        public event EventHandler TermReady;

        public TermPTYProxy()
        {
            EnsureServerRunning();
            _instanceId = _service.CreateInstance();
        }

        private static void EnsureServerRunning()
        {
            lock (_lock)
            {
                if (_serverProcess == null || _serverProcess.HasExited)
                {
                    var serverPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BendConsoleHost.exe");
                    _serverProcess = Process.Start(serverPath, Process.GetCurrentProcess().Id.ToString());

                    var binding = new NetNamedPipeBinding() { MaxReceivedMessageSize = 1024 * 1024 };
                    _factory = new DuplexChannelFactory<ITermPTYService>(
                        new InstanceContext(new TermPTYProxy()),
                        binding,
                        new EndpointAddress("net.pipe://localhost/TermPTYService"));

                    _service = _factory.CreateChannel();
                }
            }
        }

        public void StartCmd(string command, int consoleWidth = 80, int consoleHeight = 30)
        {
            _service.StartCmd(_instanceId, command, consoleWidth, consoleHeight);
        }

        void ITerminalConnection.Start()
        {
            _service.Start(_instanceId);
        }
        void ITerminalConnection.WriteInput(string data)
        {
            _service.WriteInput(_instanceId, data);
        }

        void ITerminalConnection.Resize(uint height, uint width)
        {
            _service.Resize(_instanceId, (int)width, (int)height);
        }

        void ITerminalConnection.Close()
        {
            _service.Close(_instanceId);
        }

        void ITermPTYCallback.OnTerminalOutput(Guid instanceId, string output)
        {
            if (instanceId == _instanceId)
                TerminalOutput?.Invoke(this, new TerminalOutputEventArgs(output));
        }

        void ITermPTYCallback.OnTermReady(Guid instanceId)
        {
            if (instanceId == _instanceId)
                TermReady?.Invoke(this, EventArgs.Empty);
        }

        ~TermPTYProxy()
        {
            ((ITerminalConnection)this).Close();
        }
    }
}
