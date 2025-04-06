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
        public bool TermProcIsStarted { get; private set; }

        public TermPTYProxy()
        {
            EnsureServerRunning();

            UInt32 max_connect_try = 10;
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
                    max_connect_try = 0;
                }
                catch (System.ServiceModel.EndpointNotFoundException ex)
                {
                    Task.Delay(100);
                    max_connect_try--;
                }
            } while (max_connect_try > 0);
        }

        private static void EnsureServerRunning()
        {
            lock (_lock)
            {
                if (_serverProcess == null || _serverProcess.HasExited)
                {
                    var serverPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BendConsoleHost.dat");
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = serverPath,
                        Arguments = Process.GetCurrentProcess().Id.ToString(),
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    _serverProcess = Process.Start(startInfo);
                }
            }
        }

        public void StartCmd(string command, int consoleWidth = 80, int consoleHeight = 30)
        {
            _service.StartCmd(_instanceId, command, consoleWidth, consoleHeight);
            TermProcIsStarted = true;
        }

        void ITerminalConnection.Start()
        {
            //_service.Start(_instanceId);
        }
        void ITerminalConnection.WriteInput(string data)
        {
            _service.WriteInput(_instanceId, data);
        }

        public void Resize(int height, int width)
        {
            Task.Run(() => _service.Resize(_instanceId, width, height));
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
            Task.Run(() => _service.Resize(_instanceId, (int)width, (int)height));
        }

        void ITerminalConnection.Close()
        {
            _service.Close(_instanceId);
        }

        void ITermPTYCallback.OnTerminalOutput(Guid instanceId, string output)
        {
            if (instanceId == _instanceId)
            {
                Task.Run(() => TerminalOutput?.Invoke(this, new TerminalOutputEventArgs(output)));
            }
        }

        void ITermPTYCallback.OnTermReady(Guid instanceId)
        {
            if (instanceId == _instanceId)
            {
                Task.Run(() => TermReady?.Invoke(this, EventArgs.Empty));
            }
        }

        ~TermPTYProxy()
        {
            ((ITerminalConnection)this).Close();
        }
    }
}
