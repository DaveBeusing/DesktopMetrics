using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DesktopMetrics
{
	public static class WallpaperHelper
	{
		private const int SMTO_NORMAL = 0x0000;
		private const uint WM_SPAWN_WORKER = 0x052C;
		private const int SW_SHOW = 5;

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string? className, string? windowTitle);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
		internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr SendMessageTimeout(
			IntPtr hWnd,
			uint Msg,
			IntPtr wParam,
			IntPtr lParam,
			uint fuFlags,
			uint uTimeout,
			out IntPtr lpdwResult);

		public static void AttachWindowToWallpaper(IntPtr hwnd)
		{
			// 1) Progman holen
			IntPtr progman = FindWindow("Progman", null);
			if (progman == IntPtr.Zero)
			{
	#if DEBUG
				System.Diagnostics.Debug.WriteLine("Progman not found.");
	#endif
				return;
			}

			// 2) WorkerW erzeugen (klassischer 0x052C-Trick)
			IntPtr result;
			SendMessageTimeout(
				progman,
				WM_SPAWN_WORKER,
				IntPtr.Zero,
				IntPtr.Zero,
				SMTO_NORMAL,
				1000,
				out result);

			// 3) SHELLDLL_DefView finden, dann den nächstfolgenden WorkerW
			IntPtr workerw = IntPtr.Zero;
			IntPtr defView = IntPtr.Zero;

			EnumWindows((topHandle, lParam) =>
			{
				IntPtr shellView = FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
				if (shellView != IntPtr.Zero)
				{
					defView = shellView;
					workerw = FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
					return false; // Stop enumeration
				}
				return true;
			}, IntPtr.Zero);

	#if DEBUG
			System.Diagnostics.Debug.WriteLine($"Progman: {progman}");
			System.Diagnostics.Debug.WriteLine($"DefView: {defView}");
			System.Diagnostics.Debug.WriteLine($"WorkerW: {workerw}");
	#endif

			if (workerw == IntPtr.Zero)
			{
				// Fallback
				workerw = progman;
			}

			SetParent(hwnd, workerw);
			ShowWindow(hwnd, SW_SHOW);
		}
	}
}
