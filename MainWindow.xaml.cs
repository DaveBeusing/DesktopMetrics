// Copyright (c) 2025 Dave Beusing <david.beusing@gmail.com>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.


using System;
using System.Threading;
using System.Threading.Tasks;
//using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DesktopMetrics
{
	public partial class MainWindow : Window
	{
		private TrayIconService _tray;
		private readonly DispatcherTimer _timer;
		private readonly HardwareMonitorService _hwService;
		private readonly SemaphoreSlim _refreshGate = new(1, 1); // schützt vor parallelen Refreshes
		private bool _isWallpaper = false;
		public MainWindow()
		{
			InitializeComponent();
			Positioning();
			_hwService = new HardwareMonitorService();
			_timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(1) // refresh 1 sec
			};
			_timer.Tick += async (_, __) => await RefreshMetricsAsync();
			Loaded += async (_, __) =>
			{
				this.UpdateLayout();
				Positioning(); //??
				await RefreshMetricsAsync();
				_timer.Start();
			};
			KeyDown += MainWindow_KeyDown;

			_tray = new TrayIconService();

			_tray.OnOpenRequested += () =>
			{
				this.Show();
				this.WindowState = WindowState.Normal;
				this.Activate();
			};

			_tray.OnSettingsRequested += () =>
			{
				System.Windows.MessageBox.Show("Einstellungen kommen hier hin!", "Settings");
			};

			_tray.OnExitRequested += () =>
			{
				_tray.Dispose();
				this.Close();
			};

			//Debugging
			this.Visibility = Visibility.Visible;
			this.WindowState = WindowState.Normal;
			this.ShowInTaskbar = true;
			this.Topmost = true;
			this.Activate();


		}
		private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.F10)
			{
				ToggleWallpaperMode();
			}
			else if (e.Key == Key.F9)
			{
				string file = _hwService.DumpAllSensorsToFile();
				StatusText.Text = $"Sensor-Dump gespeichert: {file}";
			}
			else if (e.Key == Key.Escape)
			{
				Close();
			}
		}
		private void ToggleWallpaperMode()
		{
			var hwnd = new WindowInteropHelper(this).Handle;
			if (!_isWallpaper)
			{
				// In Wallpaper-Modus: an WorkerW hängen
				WallpaperHelper.AttachWindowToWallpaper(hwnd);
				ShowInTaskbar = false;
				Topmost = false;
				_isWallpaper = true;
				//ModeText.Text = "Modus: Wallpaper";
				StatusText.Text = "Wallpaper-Modus aktiv. F10 für Normal.";
			}
			else
			{
				// Zurück zu normalem Top-Level-Fenster
				NativeMethods.DetachFromParent(hwnd);
				ShowInTaskbar = true;
				Topmost = true;
				_isWallpaper = false;
				//ModeText.Text = "Modus: Normal";
				StatusText.Text = "Normal-Modus aktiv. F10 für Wallpaper.";
			}
			this.UpdateLayout();
			Positioning();
		}
		private void Positioning()
		{
			var workArea  = SystemParameters.WorkArea;
			const double marginRight = 10; // 10px Abstand zum Rand
			const double marginTop = 20;
			double width = this.ActualWidth;
			if ( width <= 0 )
			{
				this.Width = 250;
				width = this.Width;
			}
			this.Left = workArea.Right - width - marginRight;
			this.Top  = workArea.Top + marginTop; 
		}
		private async Task RefreshMetricsAsync()
		{
			if( !await _refreshGate.WaitAsync(0) )
				return;
			try
			{
				// Sensoren im Hintergrund-Thread auslesen
				MetricsSnapshot snapshot = await Task.Run( () => ReadMetricsSnapshot() );
				// Danach sind wir wieder im UI-Thread (wegen DispatcherTimer + SynchronizationContext),
				// also können wir direkt die UI updaten:
				//CPU
				CpuTempText.Text     = snapshot.CpuTemp.HasValue   ? $"{snapshot.CpuTemp.Value:F1} °C" : "n/a";
				// CpuLoadText.Text = snapshot.CpuLoad.HasValue ? $"{snapshot.CpuLoad.Value:F0} %" : "n/a";
				CpuWattText.Text     = snapshot.CpuPower.HasValue  ? $" {snapshot.CpuPower.Value:F1} W" : "n/a";
				//GPU
				GpuTempText.Text     = snapshot.GpuTemp.HasValue   ? $"{snapshot.GpuTemp.Value:F1} °C" : "n/a";
				// GpuLoadText.Text = snapshot.GpuLoad.HasValue ? $"{snapshot.GpuLoad.Value:F0} %" : "n/a";
				GpuWattText.Text     = snapshot.GpuPower.HasValue  ? $" {snapshot.GpuPower.Value:F1} W" : "n/a";
				//Board
				MbTempText.Text      = snapshot.BoardTemp.HasValue ? $"{snapshot.BoardTemp.Value:F1} °C" : "n/a";
				//Storage
				StorageTempText.Text = snapshot.SsdTemp.HasValue   ? $"{snapshot.SsdTemp.Value:F1} °C" : "n/a";
				//Cooling
				WaterTempText.Text   = snapshot.WaterTemp.HasValue ? $"{snapshot.WaterTemp.Value:F1} °C" : "n/a";
				//PumpRpmText.Text     = snapshot.PumpRpm.HasValue   ? $"{snapshot.PumpRpm.Value:F0} RPM" : "n/a";
				//Status
				//StatusText.Text = $"Update: {DateTime.Now:HH:mm:ss}";
			}
			catch ( Exception )
			{
				StatusText.Text = "Fehler beim Lesen der Sensoren.";
			}
			finally
			{
				_refreshGate.Release();
			}
		}
		private MetricsSnapshot ReadMetricsSnapshot()
		{
			// Hier NICHT auf UI zugreifen!
			var cpuTemp = _hwService.GetCpuTemperature();
			var cpuLoad = _hwService.GetCpuLoad();
			var cpuPower = _hwService.GetCpuPower(); 
			var gpuTemp = _hwService.GetGpuTemperature();
			var gpuLoad = _hwService.GetGpuLoad();
			var gpuPower = _hwService.GetGpuPower();
			var board   = _hwService.GetMotherboardTemperature();
			var ssd     = _hwService.GetSsdTemperature();
			var water   = _hwService.GetWaterTemperature();
			var pump    = _hwService.GetPumpRpm();
			return new MetricsSnapshot(
				CpuTemp:   cpuTemp,
				CpuLoad:   cpuLoad,
				CpuPower:  cpuPower,
				GpuTemp:   gpuTemp,
				GpuLoad:   gpuLoad,
				GpuPower:  gpuPower,
				BoardTemp: board,
				SsdTemp:   ssd,
				WaterTemp: water,
				PumpRpm:   pump
			);
		}
		protected override void OnClosed(EventArgs e)
		{
			_tray.Dispose();
			base.OnClosed(e);
			_timer.Stop();
			_hwService.Dispose();
			_refreshGate.Dispose();
		}
	}
	public record MetricsSnapshot (
		float? CpuTemp,
		float? CpuLoad,
		float? CpuPower,
		float? GpuTemp,
		float? GpuLoad,
		float? GpuPower,
		float? BoardTemp,
		float? SsdTemp,
		float? WaterTemp,
		float? PumpRpm
	);
	internal static class NativeMethods
	{
		[System.Runtime.InteropServices.DllImport("user32.dll")]
		private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
		public static void DetachFromParent(IntPtr hwnd)
		{
			// 0 = wieder Top-Level-Fenster
			SetParent(hwnd, IntPtr.Zero);
		}
	}
}