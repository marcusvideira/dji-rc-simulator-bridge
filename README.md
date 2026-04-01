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
| Mode switch | D-pad (see below) |

### Mode Switch Behavior

The RC-N3 has a 3-position flight mode switch (Cinematic / Normal / Sport). The bridge supports three configurable styles for how this switch maps to Xbox D-pad inputs:

| Style | Behavior |
|---|---|
| **Pulse** (default) | Sends a brief D-pad press matching the current mode when the switch changes. Cinematic → D-pad Left, Normal → D-pad Up, Sport → D-pad Right. |
| **Single** | Sends a brief D-pad Down press on any mode change, regardless of which position. Useful when the simulator only needs a single "next mode" input. |
| **Hold** | Holds the D-pad direction continuously while the switch is in that position. Cinematic → D-pad Left, Normal → D-pad Up, Sport → D-pad Right. |

The mode style is selected on first launch via an interactive menu and saved to `config.json`. You can change it later by editing the `"mode_style"` value (`"pulse"`, `"single"`, or `"hold"`) or deleting `config.json` to trigger the selector again.

## Prerequisites

- [ViGEm Bus Driver](https://github.com/nefarius/ViGEmBus/releases) - virtual gamepad driver (one-time install)
- [DJI Assistant 2 (Consumer Drones Series)](https://www.dji.com/downloads/softwares/dji-assistant-2-consumer-drones-series) - install and close it (provides the USB VCOM driver)

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

Based on the original [DJI RC-N1 Simulator](https://github.com/IvanYaky/DJI_RC-N1_SIMULATOR_FLY_DCL) by Ivan Yakymenko, with protocol references from [mishavoloshchuk/mDjiController](https://github.com/mishavoloshchuk/mDjiController).

## License

See [LICENSE](LICENSE) for details.
