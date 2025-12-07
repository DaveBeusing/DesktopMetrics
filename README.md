# DesktopMetrics

**DesktopMetrics** ist ein kleines WPF-Tool für Windows, das deine
Systemmetriken direkt als „Live Wallpaper"-Overlay auf dem Desktop
anzeigt.

Es nutzt
[LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor),
um CPU-, GPU-, Mainboard-, SSD- und Wasserkühlungsdaten auszulesen.

## Features

-   CPU Temperatur & Load
-   GPU Temperatur & Load
-   Mainboard-Temperaturen (SuperIO)
-   SSD-Temperaturen
-   Wasserkühlung (z. B. D5Next): Water Temp & Pump RPM
-   Wallpaper-Modus (über WorkerW)
-   Transparente Minimal-UI

## Steuerung

-   **F10** -- Normal/WALLPAPER umschalten
-   **ESC** -- Beenden
-   **F9** -- Sensor-Dump erzeugen

## Build & Run

``` bash
dotnet restore
dotnet build
dotnet run
```

Für Sensorzugriff ggf. als Administrator starten.

## Single-File Release

``` bash
dotnet publish -c Release
```

## Projektstruktur

-   `MainWindow` -- UI & Hotkeys
-   `HardwareMonitorService` -- CPU/GPU/Board/SSD/Water/Pump
-   `WallpaperHelper` -- WorkerW-Einbindung
-   `SensorDump` -- Dump aller Sensoren

## Lizenz

MIT License (siehe LICENSE).
