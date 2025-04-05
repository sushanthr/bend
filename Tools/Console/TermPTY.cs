using Microsoft.Terminal.Wpf;
using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;
using Console.Internals;
using System.Linq;
using System.Runtime.InteropServices;

namespace Console
{
    public class TermPTY : ITerminalConnection
    {
        protected class InternalProcessFactory : IProcessFactory
        {
            public IProcess Start(string command, UIntPtr attributes, PseudoConsole console)
            {
                return ProcessFactory.Start(command, attributes, console);
            }
        }

        private delegate bool ConsoleEventDelegate(int eventType);
        private static class PInvoke
        {
            public const int CTRL_CLOSE_EVENT = 2;
            public static readonly UIntPtr PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = (UIntPtr)0x00020016; // Add this line

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate handler, bool add);
        }

        private static bool IsDesignMode = System.ComponentModel.DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject());

        private SafeFileHandle _consoleInputPipeWriteHandle;
        private StreamWriter _consoleInputWriter;
        private BinaryWriter _consoleInputWriterB;

        public TermPTY(int READ_BUFFER_SIZE = 1024 * 16, bool USE_BINARY_WRITER = false, IProcessFactory ProcessFactory = null)
        {
            this.READ_BUFFER_SIZE = READ_BUFFER_SIZE;
            this.USE_BINARY_WRITER = USE_BINARY_WRITER;
        }

        private bool USE_BINARY_WRITER;
        public StringBuilder ConsoleOutputLog { get; private set; }
        private static readonly Regex NewlineReduce = new Regex(@"\n\s*?\n\s*?[\s]+", RegexOptions.Singleline);
        public static readonly Regex colorStrip = new Regex(@"((\x1B\[\??[0-9;]*[a-zA-Z])|\uFEFF|\u200B|\x1B\]0;|[\a\b])", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        public string GetConsoleText(bool stripVTCodes = true)
        {
            return NewlineReduce.Replace((stripVTCodes ? StripColors(ConsoleOutputLog.ToString()) : ConsoleOutputLog.ToString()).Replace("\r", ""), "\n\n").Trim();
        }

        public static string StripColors(String str)
        {
            return colorStrip.Replace(str, "");
        }

        public FileStream ConsoleOutStream { get; private set; }
        public event EventHandler TermReady;
        public event EventHandler<TerminalOutputEventArgs> TerminalOutput;
        public bool TermProcIsStarted { get; private set; }
        public IProcess Process { get; protected set; }
        private PseudoConsole TheConsole;

        public void Start(string command, int consoleWidth = 80, int consoleHeight = 30, bool logOutput = false, IProcessFactory factory = null)
        {
            if (Process != null)
                throw new Exception("Called Start on ConPTY term after already started");

            factory = factory ?? new InternalProcessFactory();

            if (IsDesignMode)
            {
                TermProcIsStarted = true;
                if (TermReady != null) TermReady(this, EventArgs.Empty);
                return;
            }

            if (logOutput)
                ConsoleOutputLog = new StringBuilder();

            using (var inputPipe = new PseudoConsolePipe())
            using (var outputPipe = new PseudoConsolePipe())
            using (var pseudoConsole = PseudoConsole.Create(inputPipe.ReadSide, outputPipe.WriteSide, consoleWidth, consoleHeight))
            using (var process = factory.Start(command, PInvoke.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, pseudoConsole))
            {
                Process = process;
                TheConsole = pseudoConsole;
                ConsoleOutStream = new FileStream(outputPipe.ReadSide, FileAccess.Read);
                TermProcIsStarted = true;

                if (TermReady != null) TermReady(this, EventArgs.Empty);

                _consoleInputPipeWriteHandle = inputPipe.WriteSide;
                var st = new FileStream(_consoleInputPipeWriteHandle, FileAccess.Write);
                if (!USE_BINARY_WRITER)
                    _consoleInputWriter = new StreamWriter(st) { AutoFlush = true };
                else
                    _consoleInputWriterB = new BinaryWriter(st);

                ReadOutputLoop();
                OnClose(() => DisposeResources(process, pseudoConsole, outputPipe, inputPipe, _consoleInputWriter));

                process.WaitForExit();
                WriteToUITerminal("Session Terminated");

                TheConsole.Dispose();
            }
        }

        public void WriteToTerm(string input)
        {
            if (IsDesignMode)
                return;
            if (TheConsole.IsDisposed)
                return;
            if (_consoleInputWriter == null && _consoleInputWriterB == null)
                throw new InvalidOperationException("There is no writer attached to a pseudoconsole. Have you called Start on this instance yet?");

            if (!USE_BINARY_WRITER)
                _consoleInputWriter.Write(input);
            else
                WriteToTermBinary(Encoding.UTF8.GetBytes(input));
        }

        public void WriteToTermBinary(byte[] input)
        {
            if (!USE_BINARY_WRITER)
            {
                WriteToTerm(Encoding.UTF8.GetString(input));
                return;
            }
            _consoleInputWriterB.Write(input);
            _consoleInputWriterB.Flush();
        }

        public delegate void InterceptDelegate(ref string str);

        public void WriteToUITerminal(string str)
        {
            if (TerminalOutput != null)
                TerminalOutput(this, new TerminalOutputEventArgs(str));
        }

        public void CloseStdinToApp()
        {
            if (_consoleInputWriter != null)
            {
                _consoleInputWriter.Close();
                _consoleInputWriter.Dispose();
            }
            if (_consoleInputWriterB != null)
            {
                _consoleInputWriterB.Close();
                _consoleInputWriterB.Dispose();
            }
            _consoleInputWriter = null;
            _consoleInputWriterB = null;
        }

        public void StopExternalTermOnly()
        {
            if (Process != null && !Process.HasExited)
                Process.Kill();
        }

        private static void OnClose(Action handler)
        {
            PInvoke.SetConsoleCtrlHandler(eventType => {
                if (eventType == PInvoke.CTRL_CLOSE_EVENT)
                {
                    handler();
                }
                return false;
            }, true);
        }

        private void DisposeResources(params IDisposable[] disposables)
        {
            foreach (var disposable in disposables)
            {
                if (disposable != null)
                    disposable.Dispose();
            }
        }

        public void Start()
        {
            if (IsDesignMode)
            {
                WriteToUITerminal("MyShell DesignMode:> Your command window content here\r\n");
                return;
            }

            Task.Run((Action)ReadOutputLoop);
        }

        protected class ReadState
        {
            public char[] entireBuffer;
            public char[] curBuffer;
            public int readChars;
        }

        public bool ReadLoopStarted = false;

        protected virtual void ReadOutputLoop()
        {
            if (ReadLoopStarted)
                return;
            ReadLoopStarted = true;

            using (StreamReader reader = new StreamReader(ConsoleOutStream))
            {
                ReadState state = new ReadState
                {
                    entireBuffer = new char[READ_BUFFER_SIZE],
                    curBuffer = new char[READ_BUFFER_SIZE]
                };

                while ((state.readChars = reader.Read(state.curBuffer, 0, state.curBuffer.Length)) != 0)
                {
                    var sendBuffer = HandleRead(ref state);

                    if (sendBuffer != null)
                    {
                        string sendString = new string(sendBuffer);
                        if (InterceptOutputToUITerminal != null)
                            InterceptOutputToUITerminal(ref sendString);
                        if (!string.IsNullOrEmpty(sendString))
                        {
                            WriteToUITerminal(sendString);
                            if (ConsoleOutputLog != null)
                                ConsoleOutputLog.Append(sendString);
                        }
                    }
                }
            }
        }

        protected virtual char[] HandleRead(ref ReadState state)
        {
            return state.curBuffer.Take(state.readChars).ToArray();
        }

        public InterceptDelegate InterceptOutputToUITerminal;
        public InterceptDelegate InterceptInputToTermApp;
        protected bool _ReadOnly;
        private int READ_BUFFER_SIZE;

        public void SetReadOnly(bool readOnly = true, bool updateCursor = true)
        {
            _ReadOnly = readOnly;
            if (updateCursor)
                SetCursorVisibility(!readOnly);
        }

        void ITerminalConnection.WriteInput(string data)
        {
            string modifiedData = data;
            if (InterceptInputToTermApp != null)
                InterceptInputToTermApp(ref modifiedData);
            if (!string.IsNullOrEmpty(modifiedData) && !_ReadOnly)
                WriteToTerm(modifiedData);
        }

        void ITerminalConnection.Resize(uint row_height, uint column_width)
        {
            if (TheConsole != null)
                TheConsole.Resize((int)column_width, (int)row_height);
        }

        public void Resize(int column_width, int row_height)
        {
            if (TheConsole != null)
                TheConsole.Resize(column_width, row_height);
        }

        public void SetCursorVisibility(bool visible)
        {
            WriteToUITerminal("\x1b[?25" + (visible ? "h" : "l"));
        }

        public void Win32DirectInputMode(bool enable)
        {
            WriteToUITerminal("\x1b[?9001" + (enable ? "h" : "l"));
        }

        public void ClearUITerminal(bool fullReset = false)
        {
            WriteToUITerminal(fullReset ? "\x001bc\x1b]104\x1b\\" : "\x1b[H\x1b[2J\u001b[3J");
        }

        void ITerminalConnection.Close()
        {
            if (TheConsole != null)
                TheConsole.Dispose();
        }
    }
}
