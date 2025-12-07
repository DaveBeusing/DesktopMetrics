// Copyright (c) 2025 ${git.defaultAuthor} <${git.defaultEmail}>
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
using System.IO;
using System.Text;
using LibreHardwareMonitor.Hardware;

namespace DesktopMetrics
{
	public static class SensorDump
	{
		public static string DumpAll(Computer computer)
		{
			var sb = new StringBuilder();
			sb.AppendLine("===== DesktopMetrics Sensor Dump =====");
			sb.AppendLine($"Zeitpunkt: {DateTime.Now}");
			sb.AppendLine("--------------------------------------");
			sb.AppendLine();
			foreach (var hw in computer.Hardware)
			{
				hw.Update();
				sb.AppendLine($"[Hardware] {hw.HardwareType} | {hw.Name}");
				// Hauptsensoren
				foreach (var sensor in hw.Sensors)
				{
					sb.AppendLine($"  Sensor: {sensor.SensorType,-12} | {sensor.Name,-30} | Value={sensor.Value}");
				}
				// Subhardware (wichtig für viele Boards + GPU-Chips)
				foreach (var sub in hw.SubHardware)
				{
					sub.Update();
					sb.AppendLine($"  [SubHardware] {sub.HardwareType} | {sub.Name}");
					foreach (var sensor in sub.Sensors)
					{
						sb.AppendLine($"    Sensor: {sensor.SensorType,-12} | {sensor.Name,-30} | Value={sensor.Value}");
					}
				}
				sb.AppendLine();
			}
			// Datei speichern
			string folder = Path.Combine( Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopMetrics");
			Directory.CreateDirectory(folder);
			string file = Path.Combine(folder, $"SensorDump_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
			File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
			return file;
		}
	}
}
