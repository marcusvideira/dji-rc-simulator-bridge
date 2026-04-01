# DJI RC Simulator Bridge

Connect your DJI RC-N3 controller to your PC and use it as a virtual Xbox 360 gamepad for drone simulators.

Available in two versions: **Python** (lightweight scripting) and **C# .NET** (native Windows app with GUI and system tray).

## Features

- Reads stick positions, gimbal scroll, buttons, and mode switch via the DJI DUML protocol
- Emulates a virtual Xbox 360 controller recognized by any simulator
- Auto-detects the DJI USB VCOM serial port
- Configurable mode switch behavior (Pulse, Single, Hold)
- Stick deadzone with full-range linear scaling
- ~100Hz gamepad update rate with 1ms Windows timer resolution

## Controller Mapping

| RC-N3 Control | Xbox 360 Gamepad |
|---|---|
| Left stick | Left joystick |
| Right stick | Right joystick |
| Gimbal scroll down | Left Trigger (LT) |
| Gimbal scroll up | Right Trigger (RT) |
| Camera shoot button | Right Bumper (RB) |
| FN button | Left Bumper (LB) |
| Camera swap button | Y button |
| RTH button | Back button |
| Mode: Cinematic | D-pad Left |
| Mode: Normal | D-pad Up |
| Mode: Sport | D-pad Right |

## Prerequisites

- [ViGEm Bus Driver](https://github.com/nefarius/ViGEmBus/releases) - virtual gamepad driver (one-time install)
- [DJI Assistant 2 (Consumer Drones Series)](https://www.dji.com/downloads/softwares/dji-assistant-2-consumer-drones-series) - install and close it (provides the USB VCOM driver)

---

## C# Version (Recommended)

Native Windows application with two distribution modes. Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) for building, or download the pre-built self-contained `.exe` from Releases (no runtime needed).

### Project Structure

```
csharp/
├── DjiRcSimBridge/              # Shared core library
│   ├── Protocol/                # DUML protocol (checksums, packet parsing)
│   ├── Serial/                  # Serial reader, port auto-detection
│   ├── Gamepad/                 # ViGEm Xbox 360 controller output
│   ├── Bridge/                  # Orchestration + dependency checker
│   ├── GUI/                     # WinForms main window + system tray
│   ├── Config/                  # JSON configuration persistence
│   └── UI/                      # Console UI (Spectre.Console)
├── DjiRcSimBridge.Console/      # Console entry point
└── DjiRcSimBridge.GUI/          # GUI entry point (WinForms + system tray)
```

### Build

```bash
cd csharp
# Build everything
build-and-run.bat

# Or build individual projects
dotnet build DjiRcSimBridge.slnx -c Release
```

### Run

**GUI mode** (system tray, live visualization):

```bash
run-gui.bat
run-gui.bat -p COM5          # explicit serial port
```

**Console mode** (terminal, live debug output):

```bash
run-console.bat
run-console.bat -p COM5      # explicit serial port
```

### Publish (Self-Contained Executables)

```bash
publish.bat
```

Produces two standalone executables in `csharp/publish/`:

| File | Description |
|---|---|
| `console/DjiRcSimBridge.exe` | Console app with live stick/button output |
| `gui/DjiRcSimBridgeGUI.exe` | GUI app with system tray support |

Both are self-contained (no .NET runtime required). Distribute the `.exe` alongside `ViGEmClient.dll`.

### First Run

On first launch, the GUI version checks for required dependencies:
- **ViGEm Bus Driver** - if missing, prompts with a download link
- **ViGEmClient.dll** - must be in the same directory as the executable

---

## Python Version

Lightweight scripting version. Good for prototyping and diagnostics.

### Requirements

- Python 3.9+
- `pip install vgamepad pyserial rich`

### Run

```bash
cd python
python main.py                # interactive mode selector
python main.py --debug        # live controller state output
python main.py -p COM5        # manual serial port
```

### Diagnostics

```bash
cd python
python capture_buttons2.py    # diagnose button-to-byte mapping
```

---

## Usage

1. Install the [ViGEm Bus Driver](https://github.com/nefarius/ViGEmBus/releases) (one-time)
2. Install [DJI Assistant 2](https://www.dji.com/downloads/softwares/dji-assistant-2-consumer-drones-series), then close it (provides USB driver)
3. Power on the RC-N3
4. Connect it to your PC via the **bottom USB-C** port
5. Run the bridge (GUI or Console)
6. Launch your simulator

## Troubleshooting

- **Serial port not detected** - make sure the controller is connected via the **bottom USB-C** connector and DJI Assistant 2 has been installed (provides the USB VCOM driver). You can specify the port manually with `-p COM5`.
- **Gamepad not recognized** - ensure the ViGEm Bus Driver is installed. The GUI version will prompt you if it's missing.
- **Buttons not responding** - run `python capture_buttons2.py` to diagnose which packet bytes correspond to each button on your controller.
- **Sticks feel laggy** - the C# version runs at ~100Hz with 1ms timer resolution. If using the Python version, make sure no other heavy processes are competing for CPU.

## Tested With

- [DCL - The Game](https://store.steampowered.com/app/964570/DCL__The_Game/)

## Credits

Based on the original [DJI RC-N1 Simulator](https://github.com/ivanyakymenko/DJI_RC-N1_SIMULATOR_FLY_DCL) by Ivan Yakymenko, with protocol references from [mishavoloshchuk/mDjiController](https://github.com/mishavoloshchuk/mDjiController).

## License

See [LICENSE](LICENSE) for details.
