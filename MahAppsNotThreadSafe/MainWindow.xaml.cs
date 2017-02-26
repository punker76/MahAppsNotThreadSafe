using System;
using System.Diagnostics;


namespace MahAppsNotThreadSafe
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow
	{
		public MainWindow() {
			InitializeComponent();
		}


		private void newMetroWindowOnStaThread_Click(object sender, System.Windows.RoutedEventArgs e) {
			TestMetroWindow newWindow = WindowHelper.CreateWindowOnNewStaThread<TestMetroWindow, object>(
					args => {
						TestMetroWindow window = new TestMetroWindow();
						window.Show();
						return window;
					},
					null,
					out Exception error);
			if (newWindow == null)
				Trace.TraceError("WindowHelper returned a null MetroWindow.");
			if (error != null)
				Trace.TraceError("WindowHelper returned an error for a MetroWindow: {0}", error);
		}

		private void newWpfWindowOnStaThread_Click(object sender, System.Windows.RoutedEventArgs e) {
			WpfWindow newWindow = WindowHelper.CreateWindowOnNewStaThread<WpfWindow, object>(
					args => {
						WpfWindow window = new WpfWindow();
						window.Show();
						return window;
					},
					null,
					out Exception error);
			if (newWindow == null)
				Trace.TraceError("WindowHelper returned a null Wpf Window.");
			if (error != null)
				Trace.TraceError("WindowHelper returned an error for a Wpf Window: {0}", error);
		}


		private void newMetroWindowOnMainThread_Click(object sender, System.Windows.RoutedEventArgs e) {
			TestMetroWindow window = new TestMetroWindow();
			window.Show();
		}

		private void newWpfWindowOnMainThread_Click(object sender, System.Windows.RoutedEventArgs e) {
			WpfWindow window = new WpfWindow();
			window.Show();
		}
	}
}
