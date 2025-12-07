using System;
using System.Threading.Tasks;
using System.Windows;

namespace DesktopMetrics
{
	public partial class App : Application
	{
		public App()
		{
			// Globale Exception-Handler
			this.DispatcherUnhandledException += (_, e) =>
			{
				#if DEBUG
					MessageBox.Show(e.Exception.ToString(), "UI Exception");
				#endif
				e.Handled = true;
			};

			AppDomain.CurrentDomain.UnhandledException += (_, e) =>
			{
				#if DEBUG
					MessageBox.Show(e.ExceptionObject?.ToString() ?? "Unknown error", "UnhandledException");
				#endif
			};

			TaskScheduler.UnobservedTaskException += (_, e) =>
			{
				#if DEBUG
					MessageBox.Show(e.Exception.ToString(), "Task Exception");
				#endif
				e.SetObserved();
			};
		}
	}
}