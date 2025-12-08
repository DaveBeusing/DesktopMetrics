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
using System.Diagnostics;
using System.Drawing;
using WinForms = System.Windows.Forms;

namespace DesktopMetrics
{
	public class TrayIconService : IDisposable
	{
		private readonly WinForms.NotifyIcon _notifyIcon;
		public event Action? OnOpenRequested;
		public event Action? OnSettingsRequested;
		public event Action? OnExitRequested;
		public TrayIconService()
		{
			string exePath = Process.GetCurrentProcess().MainModule!.FileName;
			Icon appIcon = Icon.ExtractAssociatedIcon(exePath)!;
			_notifyIcon = new WinForms.NotifyIcon
			{
				Icon = appIcon,
				Text = "DesktopMetrics",
				Visible = true
			};
			var menu = new WinForms.ContextMenuStrip();
			menu.Items.Add("Öffnen / Anzeigen").Click += (_, __) => OnOpenRequested?.Invoke();
			menu.Items.Add("Einstellungen").Click += (_, __) => OnSettingsRequested?.Invoke();
			menu.Items.Add(new WinForms.ToolStripSeparator());
			menu.Items.Add("Beenden").Click += (_, __) => OnExitRequested?.Invoke();
			_notifyIcon.ContextMenuStrip = menu;
			// Linksklick: Fenster öffnen
			_notifyIcon.MouseClick += (s, e) =>
			{
				if (e.Button == WinForms.MouseButtons.Left)
					OnOpenRequested?.Invoke();
			};
		}
		public void Dispose()
		{
			_notifyIcon.Visible = false;
			_notifyIcon.Dispose();
		}
	}
}