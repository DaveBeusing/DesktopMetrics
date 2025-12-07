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
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DesktopMetrics
{
	public partial class MainWindow : Window
	{
		private readonly DispatcherTimer _timer;
		private readonly HardwareMonitorService _hwService;
		private bool _isWallpaper = false;

		public MainWindow()
		{
			InitializeComponent();
			_hwService = new HardwareMonitorService();
			_timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(1)
			};
			_timer.Tick += (_, __) => RefreshMetrics();
			Loaded += (_, __) =>
			{
				RefreshMetrics();
				_timer.Start();
			};
			KeyDown += MainWindow_KeyDown;
		}

		private void MainWindow_KeyDown(object sender, KeyEventArgs e)
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
				ModeText.Text = "Modus: Wallpaper";
				StatusText.Text = "Wallpaper-Modus aktiv. F10 für Normal.";
			}
			else
			{
				// Zurück zu normalem Top-Level-Fenster
				NativeMethods.DetachFromParent(hwnd);
				ShowInTaskbar = true;
				Topmost = true;
				_isWallpaper = false;
				ModeText.Text = "Modus: Normal";
				StatusText.Text = "Normal-Modus aktiv. F10 für Wallpaper.";
			}
		}

		private void RefreshMetrics()
		{
			try
			{
				var cpuTemp = _hwService.GetCpuTemperature();
				var cpuLoad = _hwService.GetCpuLoad();
				var gpuTemp = _hwService.GetGpuTemperature();
				var gpuLoad = _hwService.GetGpuLoad();
				var board   = _hwService.GetMotherboardTemperature();
				var ssd     = _hwService.GetSsdTemperature();
				var water   = _hwService.GetWaterTemperature();
				var pump    = _hwService.GetPumpRpm();
				CpuTempText.Text   = cpuTemp.HasValue ? $"{cpuTemp.Value:F1} °C" : "n/a";
				// optional: CPU-Load dazu
				GpuTempText.Text   = gpuTemp.HasValue ? $"{gpuTemp.Value:F1} °C" : "n/a";
				MbTempText.Text    = board.HasValue   ? $"{board.Value:F1} °C"   : "n/a";
				StorageTempText.Text = ssd.HasValue   ? $"{ssd.Value:F1} °C"     : "n/a";
				WaterTempText.Text = water.HasValue   ? $"{water.Value:F1} °C"   : "n/a";
				PumpRpmText.Text   = pump.HasValue    ? $"{pump.Value:F0} RPM"   : "n/a";
				StatusText.Text = $"Update: {DateTime.Now:HH:mm:ss}";
			}
			catch (Exception ex)
			{
				StatusText.Text = "Fehler beim Lesen der Sensoren.";
			}
		}

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);
			_timer.Stop();
			_hwService.Dispose();
		}
	}

	internal static class NativeMethods
	{
		[DllImport("user32.dll")]
		private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
		public static void DetachFromParent(IntPtr hwnd)
		{
			// 0 = wieder Top-Level-Fenster
			SetParent(hwnd, IntPtr.Zero);
		}
	}

}