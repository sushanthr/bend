using System;
using System.ComponentModel;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using Console.Internals;
using Microsoft.Terminal.Wpf;

using System.Diagnostics;


namespace Console {
	public class TerminalControl : UserControl {
		public event EventHandler TermExited;
		private bool _startRequested;
		/// <summary>
		/// Converts Color to COLOREF, note that COLOREF does not support alpha channels so it is ignored
		/// </summary>
		/// <param name="color"></param>
		/// <returns></returns>
		public static uint ColorToVal(Color color) => BitConverter.ToUInt32(new byte[] { color.R, color.G, color.B, 0 }, 0);
		public TerminalControl() {
			InitializeComponent();
			SetKBCaptureOptions();
		}

		[Flags]
		[System.ComponentModel.TypeConverter(typeof(System.ComponentModel.EnumConverter))]
		public enum INPUT_CAPTURE { None = 1 << 0, TabKey = 1 << 1, DirectionKeys = 1 << 2 };



		private static void InputCaptureChanged(DependencyObject target, DependencyPropertyChangedEventArgs e) {
			var cntrl = target as TerminalControl;
			cntrl.SetKBCaptureOptions();
		}
		private void SetKBCaptureOptions() {
			KeyboardNavigation.SetTabNavigation(this, InputCapture.HasFlag(INPUT_CAPTURE.TabKey) ? KeyboardNavigationMode.Contained : KeyboardNavigationMode.Continue);
			KeyboardNavigation.SetDirectionalNavigation(this, InputCapture.HasFlag(INPUT_CAPTURE.DirectionKeys) ? KeyboardNavigationMode.Contained : KeyboardNavigationMode.Continue);
		}
		/// <summary>
		/// Helper property for setting KeyboardNavigation.Set*Navigation commands to prevent arrow keys or tabs from causing us to leave the control (aka pass through to conpty)
		/// </summary>
		public INPUT_CAPTURE InputCapture {
			get => (INPUT_CAPTURE)GetValue(InputCaptureProperty);
			set => SetValue(InputCaptureProperty, value);
		}

		[Description("Write only, sets the terminal theme"), Category("Common")]
		public TerminalTheme? Theme { set => SetTheme(_Theme = value); private get => _Theme; }
		private TerminalTheme? _Theme;
		private void SetTheme(TerminalTheme? v) { if (v != null) Terminal?.SetTheme(v.Value, FontFamilyWhenSettingTheme.Source, (short)FontSizeWhenSettingTheme); }



		[Description("Write only, When true user cannot give input through the Terminal UI (can still write to the Term from code behind using Term.WriteToTerm)"), Category("Common")]
		public bool? IsReadOnly { set => SetReadOnly(_IsReadOnly = value); private get => _IsReadOnly; }
		private bool? _IsReadOnly;
		private void SetReadOnly(bool? v) { if (v != null) ConPTYTerm?.SetReadOnly(v.Value, false); }//no cursor auto update if user wants that they can use the separate dependency property for the cursor visibility

		[Description("Write only, if the type cursor shows on the Terminal UI"), Category("Common")]
		public bool? IsCursorVisible { set => SetCursor(_IsCursorVisible = value); private get => _IsCursorVisible; }
		private bool? _IsCursorVisible;
		private void SetCursor(bool? v) { if (v != null) ConPTYTerm?.SetCursorVisibility(v.Value); }

		[Description("Direct access to the UI terminal control itself that handles rendering")]
		public Microsoft.Terminal.Wpf.TerminalControl Terminal {
			get => (Microsoft.Terminal.Wpf.TerminalControl)GetValue(TerminalPropertyKey.DependencyProperty);
			set => SetValue(TerminalPropertyKey, value);
		}

		private static void OnTermChanged(DependencyObject target, DependencyPropertyChangedEventArgs e) {
			// The terminal starts at the renderer's current dimensions. Do not resize it
			// again from TermReady: that can race cmd.exe's initial prompt and erase it.
		}
		/// <summary>
		/// Update the Term if you want to set to an existing
		/// </summary>
		[Description("The backend TermPTYProxy connection allows changing the application the control is connected to")]
		public TermPTYProxy ConPTYTerm {
			get => (TermPTYProxy)GetValue(ConPTYTermProperty);
			set => SetValue(ConPTYTermProperty, value);
		}


		public TermPTYProxy DisconnectConPTYTerm() {
			if (Terminal != null)
				Terminal.Connection = null;
			var ret = ConPTYTerm;
			ConPTYTerm = null;
			return ret;
		}

		public string StartupCommandLine {
			get => (string)GetValue(StartupCommandLineProperty);
			set => SetValue(StartupCommandLineProperty, value);
		}

		public bool LogConPTYOutput {
			get => (bool)GetValue(LogConPTYOutputProperty);
			set => SetValue(LogConPTYOutputProperty, value);
		}
		/// <summary>
		/// Sets if the GUI Terminal control communicates to ConPTY using extended key events (handles certain control sequences better)
		/// https://github.com/microsoft/terminal/blob/main/doc/specs/%234999%20-%20Improved%20keyboard%20handling%20in%20Conpty.md
		/// </summary>
		public bool Win32InputMode {
			get => (bool)GetValue(Win32InputModeProperty);
			set => SetValue(Win32InputModeProperty, value);
		}

		public FontFamily FontFamilyWhenSettingTheme {
			get => (FontFamily)GetValue(FontFamilyWhenSettingThemeProperty);
			set => SetValue(FontFamilyWhenSettingThemeProperty, value);
		}

		public int FontSizeWhenSettingTheme {
			get => (int)GetValue(FontSizeWhenSettingThemeProperty);
			set => SetValue(FontSizeWhenSettingThemeProperty, value);
		}
		private void InitializeComponent() {
            Terminal = new Microsoft.Terminal.Wpf.TerminalControl();
			Terminal.AutoResize = true;
			Terminal.Loaded += Terminal_Loaded;
			this.IsVisibleChanged += TerminalControl_IsVisibleChanged;
			var grid = new Grid() { };
			grid.Children.Add(Terminal);
			this.Content = grid;
			Focusable = true;
			Terminal.Focusable = true;
            this.GotFocus += EasyTerminalControl_GotFocus;
		}

        private void EasyTerminalControl_GotFocus(object sender, RoutedEventArgs e)
        {
			Terminal.Focus();
        }

		void MainThreadRun(Action action) {
			if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
				Dispatcher.BeginInvoke(action);
		}

		private void StartTerm(int column_width, int row_height) {
			if (_startRequested)
				return;
			_startRequested = true;
			if (ConPTYTerm == null) {
				try {
					ConPTYTerm = new TermPTYProxy();
					ConPTYTerm.TermExited += (sender, args) => MainThreadRun(() => TermExited?.Invoke(this, EventArgs.Empty));
				}
				catch (Exception exception) {
					Debug.WriteLine("Console integration is unavailable: " + exception);
					_startRequested = false;
					return;
				}
			}

			if (ConPTYTerm.TermProcIsStarted) {
				ConPTYTerm.Resize(column_width, row_height);
				return;
			}
			var cmd = StartupCommandLine;
			var term = ConPTYTerm;
			Terminal.Connection = term;
			term.Win32DirectInputMode(Win32InputMode);
			Task.Run(() => term.StartCmd(cmd, column_width, row_height));
		}
		private void Terminal_Loaded(object sender, RoutedEventArgs e) {
			if (IsVisible)
				StartTerm(Terminal.Columns, Terminal.Rows);
			SetTheme(Theme);
			SetCursor(IsCursorVisible);
			SetReadOnly(IsReadOnly);
			SetCursor(IsCursorVisible);
		}

		private void TerminalControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
			if (IsVisible && Terminal != null && Terminal.IsLoaded)
				StartTerm(Terminal.Columns, Terminal.Rows);
		}

		public void StartTerminal() {
			if (Terminal != null)
				StartTerm(Terminal.Columns, Terminal.Rows);
		}

		public void ResizeToCurrentDimensions() {
			if (Terminal != null && ConPTYTerm != null && ConPTYTerm.TermProcIsStarted)
				ConPTYTerm.Resize(Terminal.Rows, Terminal.Columns);
		}

		#region Depdendency Properties
		public static readonly DependencyProperty InputCaptureProperty = DependencyProperty.Register(nameof(InputCapture), typeof(INPUT_CAPTURE), typeof(TerminalControl), new
		PropertyMetadata(INPUT_CAPTURE.TabKey | INPUT_CAPTURE.DirectionKeys, InputCaptureChanged));

		public static readonly DependencyProperty ThemeProperty = PropHelper.GenerateWriteOnlyProperty((c) => c.Theme);

		protected static readonly DependencyPropertyKey TerminalPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Terminal), typeof(Microsoft.Terminal.Wpf.TerminalControl), typeof(TerminalControl), new PropertyMetadata());
		public static readonly DependencyProperty TerminalProperty = TerminalPropertyKey.DependencyProperty;

		public static readonly DependencyProperty ConPTYTermProperty = DependencyProperty.Register(nameof(ConPTYTerm), typeof(TermPTYProxy), typeof(TerminalControl), new PropertyMetadata(null, OnTermChanged));
		public static readonly DependencyProperty StartupCommandLineProperty = DependencyProperty.Register(nameof(StartupCommandLine), typeof(string), typeof(TerminalControl), new PropertyMetadata("powershell.exe"));

		public static readonly DependencyProperty LogConPTYOutputProperty = DependencyProperty.Register(nameof(LogConPTYOutput), typeof(bool), typeof(TerminalControl), new PropertyMetadata(false));
		public static readonly DependencyProperty Win32InputModeProperty = DependencyProperty.Register(nameof(Win32InputMode), typeof(bool), typeof(TerminalControl), new PropertyMetadata(true));
		public static readonly DependencyProperty IsReadOnlyProperty = PropHelper.GenerateWriteOnlyProperty((c) => c.IsReadOnly);
		public static readonly DependencyProperty IsCursorVisibleProperty = PropHelper.GenerateWriteOnlyProperty((c) => c.IsCursorVisible);

		public static readonly DependencyProperty FontFamilyWhenSettingThemeProperty = DependencyProperty.Register(nameof(FontFamilyWhenSettingTheme), typeof(FontFamily), typeof(TerminalControl), new PropertyMetadata(new FontFamily("Cascadia Code")));

		public static readonly DependencyProperty FontSizeWhenSettingThemeProperty = DependencyProperty.Register(nameof(FontSizeWhenSettingTheme), typeof(int), typeof(TerminalControl), new PropertyMetadata(10));

		private class PropHelper : DepPropHelper<TerminalControl> { }

		#endregion
	}
}
