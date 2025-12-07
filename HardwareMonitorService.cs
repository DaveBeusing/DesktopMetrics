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
using System.Linq;
using System.Collections.Generic;
using LibreHardwareMonitor.Hardware;

namespace DesktopMetrics
{
	public class HardwareMonitorService : IVisitor, IDisposable
	{
		private readonly Computer _computer;
		private readonly object _lock = new();
		private bool _disposed;
		public HardwareMonitorService()
		{
			_computer = new Computer
			{
				IsCpuEnabled = true,
				IsGpuEnabled = true,
				IsMotherboardEnabled = true,
				IsStorageEnabled = true,
				IsMemoryEnabled = true,
				IsControllerEnabled = true,
				IsNetworkEnabled = true,
				IsPsuEnabled = true
			};
			_computer.Open();
			_computer.Accept(this);
		}
		public void Dispose()
		{
			if (_disposed) return;
			lock (_lock)
			{
				_computer.Close();
				_disposed = true;
			}
		}
		// IVisitor
		public void VisitComputer(IComputer computer)
		{
			foreach (var hw in computer.Hardware)
				hw.Accept(this);
		}
		public void VisitHardware(IHardware hardware)
		{
			hardware.Update();
			foreach (var sub in hardware.SubHardware)
				sub.Accept(this);
		}
		public void VisitSensor(ISensor sensor) { }
		public void VisitParameter(IParameter parameter) { }
		// --- Public API ---
		public float? GetCpuTemperature() => GetCpuTemperatureInternal();
		public float? GetCpuLoad() => GetSensorValue( HardwareType.Cpu, SensorType.Load, "CPU Package" );
		public float? GetCpuPower() => GetSensorValue( HardwareType.Cpu, SensorType.Power, "CPU Package" );
		public float? GetGpuTemperature() => GetGpuTemperatureInternal();
		public float? GetGpuLoad() => GetSensorValue( HardwareType.GpuNvidia, SensorType.Load, "GPU Package" );
		public float? GetGpuPower() => GetSensorValue( HardwareType.GpuNvidia, SensorType.Power, "GPU Package" );
		public float? GetWaterTemperature() => GetFromCooler( SensorType.Temperature, "Water Temperature" );
		public float? GetPumpRpm() => GetFromCooler( SensorType.Fan, "Pump" );
		public float? GetMotherboardTemperature() => GetMotherboardTemperatureInternal();
		public float? GetSsdTemperature() => GetMaxStorageTemperature();
		private float? GetCpuTemperatureInternal()
		{
			// Reihenfolge: CPU Package -> Core Max -> Core Average
			var names = new[] { "CPU Package", "Core Max", "Core Average" };
			return GetSensorValue( HardwareType.Cpu, SensorType.Temperature, names );
		}
		private float? GetGpuTemperatureInternal()
		{
			lock (_lock)
			{
				var hw = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuNvidia);
				if (hw == null) return null;
				hw.Update();
				var sensors = EnumerateSensors(hw, SensorType.Temperature).ToList();
				if (sensors.Count == 0)
					return null;
				// GPU Memory Junction 255 °C rausfiltern
				var plausible = sensors.Where(s => s.Value.HasValue && IsPlausibleTemperature(s.Value.Value)).ToList();
				if (plausible.Count == 0)
					plausible = sensors.Where(s => s.Value.HasValue).ToList();
				// Priorität: Hot Spot > Core > Rest
				var ordered = plausible.OrderByDescending(s => ScoreName(s.Name, new[] { "hot spot", "hotspot", "gpu core" })).ThenByDescending(s => s.Value ?? 0);
				return ordered.FirstOrDefault()?.Value;
			}
		}
		private float? GetMotherboardTemperatureInternal()
		{
			lock (_lock)
			{
				var mb = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard);
				if (mb == null) return null;
				mb.Update();
				var tempSensors = EnumerateSensors(mb, SensorType.Temperature).Where(s => s.Value.HasValue && IsPlausibleTemperature(s.Value.Value)).ToList();
				if (tempSensors.Count == 0)
					return null;
				// Viele Boards: "Temperature #1..#6" – wir nehmen einfach den wärmsten als "Board"
				var hottest = tempSensors.OrderByDescending(s => s.Value ?? 0).FirstOrDefault();
				return hottest?.Value;
			}
		}
		private float? GetFromCooler(SensorType type, string nameContains)
		{
			lock (_lock)
			{
				var cooler = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cooler);
				if (cooler == null) return null;
				cooler.Update();
				var sensor = cooler.Sensors
					.Where(s => s.SensorType == type && s.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
					.FirstOrDefault();
				return sensor?.Value;
			}
		}
		private float? GetMaxStorageTemperature()
		{
			lock (_lock)
			{
				var temps = new List<float>();
				foreach (var hw in _computer.Hardware.Where(h => h.HardwareType == HardwareType.Storage))
				{
					hw.Update();
					var tSensors = EnumerateSensors(hw, SensorType.Temperature)
						.Where(s => s.Value.HasValue && IsPlausibleTemperature(s.Value.Value))
						.ToList();
					if (tSensors.Count > 0)
					{
						var max = tSensors.Max(s => s.Value!.Value);
						temps.Add(max);
					}
				}
				if (temps.Count == 0) return null;
				return temps.Max();
			}
		}
		private float? GetSensorValue(HardwareType type, SensorType sensorType, params string[] preferredNames)
		{
			lock (_lock)
			{
				var hw = _computer.Hardware.FirstOrDefault(h => h.HardwareType == type);
				if (hw == null) return null;
				hw.Update();
				var sensors = EnumerateSensors(hw, sensorType).Where(s => s.Value.HasValue).ToList();
				if (sensors.Count == 0)
					return null;
				var plausible = sensors.Where(s => IsPlausibleTemperature(s.Value!.Value) || sensorType != SensorType.Temperature).ToList();
				if (plausible.Count == 0)
					plausible = sensors;
				if (preferredNames is { Length: > 0 })
				{
					var ordered = plausible.OrderByDescending(s => ScoreName(s.Name, preferredNames)).ThenByDescending(s => s.Value ?? 0);
					return ordered.FirstOrDefault()?.Value;
				}
				return plausible.OrderByDescending(s => s.Value ?? 0).FirstOrDefault()?.Value;
			}
		}
		private static IEnumerable<ISensor> EnumerateSensors(IHardware hw, SensorType type)
		{
			foreach (var s in hw.Sensors.Where(s => s.SensorType == type))
				yield return s;
			foreach (var sub in hw.SubHardware)
			{
				sub.Update();
				foreach (var s in sub.Sensors.Where(s => s.SensorType == type))
					yield return s;
			}
		}
		private static bool IsPlausibleTemperature(float value)
		{
			// Filtert 0°C, 255°C, extremen Müll
			return value > 0 && value < 120;
		}
		private static int ScoreName(string? name, string[]? preferred)
		{
			if (string.IsNullOrWhiteSpace(name) || preferred == null || preferred.Length == 0)
				return 0;
			var lower = name.ToLowerInvariant();
			var score = 0;
			foreach (var key in preferred)
			{
				if (lower.Contains(key.ToLowerInvariant()))
					score += 2;
			}
			return score;
		}
		public string DumpAllSensorsToFile()
		{
			lock (_lock)
			{
				return SensorDump.DumpAll(_computer);
			}
		}
	}
}
