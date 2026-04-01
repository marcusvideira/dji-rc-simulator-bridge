"""DJI RC-N3 Simulator Bridge — maps RC-N3 controls to a virtual Xbox 360 gamepad."""

import argparse
import ctypes
import json
import msvcrt
import os
import struct
import time
from dataclasses import dataclass
from queue import Empty, SimpleQueue
from threading import Event, Thread
from typing import Union

import serial
import serial.tools.list_ports
import vgamepad as vg
from rich.console import Console
from rich.panel import Panel
from rich.table import Table

# ── Constants ────────────────────────────────────────────────────────────────

VERSION = "5.1.0"
CONFIG_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "config.json")

# Timing
GAMEPAD_UPDATE_HZ = 100
GAMEPAD_UPDATE_INTERVAL = 1.0 / GAMEPAD_UPDATE_HZ
SERIAL_TIMEOUT = 0.005
SERIAL_POLL_INTERVAL = 0.008
MODE_PULSE_DURATION = 0.15

# DUML protocol
DUML_SYNC = 0x55
DUML_SOURCE = 0x0A
DUML_TARGET = 0x06
DUML_CMD_TYPE = 0x40
DUML_CMD_SET = 0x06
DUML_CMD_STICKS = 0x01
DUML_CMD_BUTTONS = 0x27
DUML_CMD_SIM_MODE = 0x24
STICKS_PACKET_LEN = 38
BUTTONS_PACKET_LEN = 58

# Button protocol offsets (within payload starting at byte 11)
BTN_PAYLOAD_BYTE = 18
MODE_PAYLOAD_BYTE = 17
BTN_CAMERA_SHOOT_MASK = 0x60
BTN_FN_MASK = 0x02
BTN_CAMERA_SWAP_MASK = 0x04
BTN_RTH_MASK = 0x80
MODE_SPORT_VAL = 0x00
MODE_NORMAL_VAL = 0x10
MODE_CINE_VAL = 0x20

# Stick parsing
STICK_CENTER = 1024
STICK_RAW_HALF_RANGE = 660  # max raw deflection from center (364..1024..1684)
STICK_DEADZONE = 1          # raw units around center that snap to zero
STICK_EFFECTIVE_RANGE = STICK_RAW_HALF_RANGE - STICK_DEADZONE  # post-deadzone range
STICK_MIN = -32768
STICK_MAX = 32767
TRIGGER_MAX = 255


# ── Data types ───────────────────────────────────────────────────────────────

@dataclass(slots=True)
class SticksUpdate:
    """Parsed stick + gimbal positions from a 38-byte DUML response."""
    rh: int
    rv: int
    lh: int
    lv: int
    gimbal_lt: int
    gimbal_rt: int


@dataclass(slots=True)
class ButtonsUpdate:
    """Parsed button + mode state from a 58-byte DUML response."""
    camera_shoot: bool
    fn: bool
    camera_swap: bool
    rth: bool
    mode: str


ControllerUpdate = Union[SticksUpdate, ButtonsUpdate]


# ── CRC tables (precomputed, immutable) ──────────────────────────────────────

_CRC16_TABLE = (
    0x0000, 0x1189, 0x2312, 0x329B, 0x4624, 0x57AD, 0x6536, 0x74BF,
    0x8C48, 0x9DC1, 0xAF5A, 0xBED3, 0xCA6C, 0xDBE5, 0xE97E, 0xF8F7,
    0x1081, 0x0108, 0x3393, 0x221A, 0x56A5, 0x472C, 0x75B7, 0x643E,
    0x9CC9, 0x8D40, 0xBFDB, 0xAE52, 0xDAED, 0xCB64, 0xF9FF, 0xE876,
    0x2102, 0x308B, 0x0210, 0x1399, 0x6726, 0x76AF, 0x4434, 0x55BD,
    0xAD4A, 0xBCC3, 0x8E58, 0x9FD1, 0xEB6E, 0xFAE7, 0xC87C, 0xD9F5,
    0x3183, 0x200A, 0x1291, 0x0318, 0x77A7, 0x662E, 0x54B5, 0x453C,
    0xBDCB, 0xAC42, 0x9ED9, 0x8F50, 0xFBEF, 0xEA66, 0xD8FD, 0xC974,
    0x4204, 0x538D, 0x6116, 0x709F, 0x0420, 0x15A9, 0x2732, 0x36BB,
    0xCE4C, 0xDFC5, 0xED5E, 0xFCD7, 0x8868, 0x99E1, 0xAB7A, 0xBAF3,
    0x5285, 0x430C, 0x7197, 0x601E, 0x14A1, 0x0528, 0x37B3, 0x263A,
    0xDECD, 0xCF44, 0xFDDF, 0xEC56, 0x98E9, 0x8960, 0xBBFB, 0xAA72,
    0x6306, 0x728F, 0x4014, 0x519D, 0x2522, 0x34AB, 0x0630, 0x17B9,
    0xEF4E, 0xFEC7, 0xCC5C, 0xDDD5, 0xA96A, 0xB8E3, 0x8A78, 0x9BF1,
    0x7387, 0x620E, 0x5095, 0x411C, 0x35A3, 0x242A, 0x16B1, 0x0738,
    0xFFCF, 0xEE46, 0xDCDD, 0xCD54, 0xB9EB, 0xA862, 0x9AF9, 0x8B70,
    0x8408, 0x9581, 0xA71A, 0xB693, 0xC22C, 0xD3A5, 0xE13E, 0xF0B7,
    0x0840, 0x19C9, 0x2B52, 0x3ADB, 0x4E64, 0x5FED, 0x6D76, 0x7CFF,
    0x9489, 0x8500, 0xB79B, 0xA612, 0xD2AD, 0xC324, 0xF1BF, 0xE036,
    0x18C1, 0x0948, 0x3BD3, 0x2A5A, 0x5EE5, 0x4F6C, 0x7DF7, 0x6C7E,
    0xA50A, 0xB483, 0x8618, 0x9791, 0xE32E, 0xF2A7, 0xC03C, 0xD1B5,
    0x2942, 0x38CB, 0x0A50, 0x1BD9, 0x6F66, 0x7EEF, 0x4C74, 0x5DFD,
    0xB58B, 0xA402, 0x9699, 0x8710, 0xF3AF, 0xE226, 0xD0BD, 0xC134,
    0x39C3, 0x284A, 0x1AD1, 0x0B58, 0x7FE7, 0x6E6E, 0x5CF5, 0x4D7C,
    0xC60C, 0xD785, 0xE51E, 0xF497, 0x8028, 0x91A1, 0xA33A, 0xB2B3,
    0x4A44, 0x5BCD, 0x6956, 0x78DF, 0x0C60, 0x1DE9, 0x2F72, 0x3EFB,
    0xD68D, 0xC704, 0xF59F, 0xE416, 0x90A9, 0x8120, 0xB3BB, 0xA232,
    0x5AC5, 0x4B4C, 0x79D7, 0x685E, 0x1CE1, 0x0D68, 0x3FF3, 0x2E7A,
    0xE70E, 0xF687, 0xC41C, 0xD595, 0xA12A, 0xB0A3, 0x8238, 0x93B1,
    0x6B46, 0x7ACF, 0x4854, 0x59DD, 0x2D62, 0x3CEB, 0x0E70, 0x1FF9,
    0xF78F, 0xE606, 0xD49D, 0xC514, 0xB1AB, 0xA022, 0x92B9, 0x8330,
    0x7BC7, 0x6A4E, 0x58D5, 0x495C, 0x3DE3, 0x2C6A, 0x1EF1, 0x0F78,
)

_CRC8_TABLE = (
    0x00, 0x5E, 0xBC, 0xE2, 0x61, 0x3F, 0xDD, 0x83,
    0xC2, 0x9C, 0x7E, 0x20, 0xA3, 0xFD, 0x1F, 0x41,
    0x9D, 0xC3, 0x21, 0x7F, 0xFC, 0xA2, 0x40, 0x1E,
    0x5F, 0x01, 0xE3, 0xBD, 0x3E, 0x60, 0x82, 0xDC,
    0x23, 0x7D, 0x9F, 0xC1, 0x42, 0x1C, 0xFE, 0xA0,
    0xE1, 0xBF, 0x5D, 0x03, 0x80, 0xDE, 0x3C, 0x62,
    0xBE, 0xE0, 0x02, 0x5C, 0xDF, 0x81, 0x63, 0x3D,
    0x7C, 0x22, 0xC0, 0x9E, 0x1D, 0x43, 0xA1, 0xFF,
    0x46, 0x18, 0xFA, 0xA4, 0x27, 0x79, 0x9B, 0xC5,
    0x84, 0xDA, 0x38, 0x66, 0xE5, 0xBB, 0x59, 0x07,
    0xDB, 0x85, 0x67, 0x39, 0xBA, 0xE4, 0x06, 0x58,
    0x19, 0x47, 0xA5, 0xFB, 0x78, 0x26, 0xC4, 0x9A,
    0x65, 0x3B, 0xD9, 0x87, 0x04, 0x5A, 0xB8, 0xE6,
    0xA7, 0xF9, 0x1B, 0x45, 0xC6, 0x98, 0x7A, 0x24,
    0xF8, 0xA6, 0x44, 0x1A, 0x99, 0xC7, 0x25, 0x7B,
    0x3A, 0x64, 0x86, 0xD8, 0x5B, 0x05, 0xE7, 0xB9,
    0x8C, 0xD2, 0x30, 0x6E, 0xED, 0xB3, 0x51, 0x0F,
    0x4E, 0x10, 0xF2, 0xAC, 0x2F, 0x71, 0x93, 0xCD,
    0x11, 0x4F, 0xAD, 0xF3, 0x70, 0x2E, 0xCC, 0x92,
    0xD3, 0x8D, 0x6F, 0x31, 0xB2, 0xEC, 0x0E, 0x50,
    0xAF, 0xF1, 0x13, 0x4D, 0xCE, 0x90, 0x72, 0x2C,
    0x6D, 0x33, 0xD1, 0x8F, 0x0C, 0x52, 0xB0, 0xEE,
    0x32, 0x6C, 0x8E, 0xD0, 0x53, 0x0D, 0xEF, 0xB1,
    0xF0, 0xAE, 0x4C, 0x12, 0x91, 0xCF, 0x2D, 0x73,
    0xCA, 0x94, 0x76, 0x28, 0xAB, 0xF5, 0x17, 0x49,
    0x08, 0x56, 0xB4, 0xEA, 0x69, 0x37, 0xD5, 0x8B,
    0x57, 0x09, 0xEB, 0xB5, 0x36, 0x68, 0x8A, 0xD4,
    0x95, 0xCB, 0x29, 0x77, 0xF4, 0xAA, 0x48, 0x16,
    0xE9, 0xB7, 0x55, 0x0B, 0x88, 0xD6, 0x34, 0x6A,
    0x2B, 0x75, 0x97, 0xC9, 0x4A, 0x14, 0xF6, 0xA8,
    0x74, 0x2A, 0xC8, 0x96, 0x15, 0x4B, 0xA9, 0xF7,
    0xB6, 0xE8, 0x0A, 0x54, 0xD7, 0x89, 0x6B, 0x35,
)


# ── Checksum functions ───────────────────────────────────────────────────────

def calc_crc16(packet: bytes | bytearray, length: int) -> int:
    """CRC-16 used for DUML packet validation (seed 0x3692)."""
    v = 0x3692
    for i in range(length):
        v = (v >> 8) ^ _CRC16_TABLE[(packet[i] ^ v) & 0xFF]
    return v


def calc_header_crc8(packet: bytes | bytearray, length: int, seed: int = 0x77) -> int:
    """CRC-8 used for the 3-byte DUML header."""
    v = seed
    for i in range(length):
        v = _CRC8_TABLE[(packet[i] ^ v) & 0xFF]
    return v


# ── Packet parsing ───────────────────────────────────────────────────────────

def _apply_deadzone(centered: int) -> int:
    """Snap values within STICK_DEADZONE of center to zero, then rescale
    so the remaining range still reaches full deflection."""
    if abs(centered) <= STICK_DEADZONE:
        return 0
    if centered > 0:
        return centered - STICK_DEADZONE
    return centered + STICK_DEADZONE


def parse_stick_value(raw_bytes: bytes) -> int:
    """Parse 2-byte LE RC channel value to Xbox axis range (-32768..32767).

    Deadzone applied near center, then rescaled so full physical deflection
    still reaches full int16 output.
    """
    centered = _apply_deadzone(
        int.from_bytes(raw_bytes, byteorder="little") - STICK_CENTER
    )
    value = centered * STICK_MAX // STICK_EFFECTIVE_RANGE
    return max(STICK_MIN, min(STICK_MAX, value))


def parse_gimbal_to_triggers(raw_bytes: bytes) -> tuple[int, int]:
    """Parse gimbal scroll to (left_trigger, right_trigger), each 0..255.

    Negative values map to left trigger, positive to right trigger.
    Deadzone applied so resting position reads exactly zero.
    """
    centered = _apply_deadzone(
        int.from_bytes(raw_bytes, byteorder="little") - STICK_CENTER
    )
    value = centered * STICK_MAX // STICK_EFFECTIVE_RANGE
    if value < 0:
        return (min(TRIGGER_MAX, abs(value) * TRIGGER_MAX // STICK_MAX), 0)
    return (0, min(TRIGGER_MAX, value * TRIGGER_MAX // STICK_MAX))


def parse_sticks_packet(data: bytearray) -> SticksUpdate:
    """Parse 38-byte channel response (cmd_id=0x01): sticks + gimbal."""
    lt, rt = parse_gimbal_to_triggers(data[25:27])
    return SticksUpdate(
        rh=parse_stick_value(data[13:15]),
        rv=parse_stick_value(data[16:18]),
        lh=parse_stick_value(data[22:24]),
        lv=parse_stick_value(data[19:21]),
        gimbal_lt=lt,
        gimbal_rt=rt,
    )


def parse_buttons_packet(data: bytearray) -> ButtonsUpdate | None:
    """Parse 58-byte button response (cmd_id=0x27): buttons + mode switch.

    Returns None if payload is too short.
    """
    payload = data[11:-2]
    if len(payload) <= BTN_PAYLOAD_BYTE:
        return None

    btn = payload[BTN_PAYLOAD_BYTE]
    mode_byte = payload[MODE_PAYLOAD_BYTE]

    if mode_byte == MODE_CINE_VAL:
        mode = "cine"
    elif mode_byte == MODE_NORMAL_VAL:
        mode = "normal"
    else:
        mode = "sport"

    return ButtonsUpdate(
        camera_shoot=bool(btn & BTN_CAMERA_SHOOT_MASK),
        fn=bool(btn & BTN_FN_MASK),
        camera_swap=bool(btn & BTN_CAMERA_SWAP_MASK),
        rth=bool(btn & BTN_RTH_MASK),
        mode=mode,
    )


# ── DUML serial I/O ─────────────────────────────────────────────────────────

class DumlConnection:
    """Manages serial communication using the DUML protocol."""

    def __init__(self, port: serial.Serial) -> None:
        self._port = port
        self._seq = 0x34EB

    def send(self, cmd_id: int, payload: bytes = b"") -> None:
        """Build and send a DUML packet with auto-incrementing sequence number."""
        packet = bytearray([DUML_SYNC])
        length = 13 + len(payload)
        packet += struct.pack("B", length & 0xFF)
        packet += struct.pack("B", (length >> 8) | 0x04)
        packet += struct.pack("B", calc_header_crc8(packet, 3))
        packet += struct.pack("B", DUML_SOURCE)
        packet += struct.pack("B", DUML_TARGET)
        packet += struct.pack("<H", self._seq)
        packet += struct.pack("B", DUML_CMD_TYPE)
        packet += struct.pack("B", DUML_CMD_SET)
        packet += struct.pack("B", cmd_id)
        packet += payload
        packet += struct.pack("<H", calc_crc16(packet, len(packet)))
        self._port.write(packet)
        self._seq = (self._seq + 1) & 0xFFFF

    def read_packet(self) -> bytearray:
        """Read one DUML packet. Returns empty bytearray if no valid sync byte."""
        b = self._port.read(1)
        if not b or b[0] != DUML_SYNC:
            return bytearray()

        header = self._port.read(2)
        if len(header) < 2:
            return bytearray()

        length = struct.unpack("<H", header)[0] & 0x03FF
        remaining = length - 3
        rest = self._port.read(remaining) if remaining > 0 else b""

        buf = bytearray([DUML_SYNC])
        buf += header
        buf += rest
        return buf

    @property
    def in_waiting(self) -> int:
        return self._port.in_waiting

    def close(self) -> None:
        self._port.close()


# ── Serial reader thread ────────────────────────────────────────────────────

def serial_reader(
    conn: DumlConnection,
    queue: SimpleQueue[ControllerUpdate],
    shutdown: Event,
) -> None:
    """Polls the RC for stick/button data and enqueues parsed updates.

    Runs in a dedicated thread. Paced by SERIAL_POLL_INTERVAL (~125Hz).
    Serial I/O is isolated here so the main thread never blocks on reads.
    """
    while not shutdown.is_set():
        loop_start = time.perf_counter()

        try:
            conn.send(DUML_CMD_STICKS)
            conn.send(DUML_CMD_BUTTONS)

            for _ in range(10):
                if conn.in_waiting == 0:
                    break

                data = conn.read_packet()
                if not data:
                    break

                cmd_id = data[10] if len(data) > 10 else 0

                if len(data) == STICKS_PACKET_LEN and cmd_id == DUML_CMD_STICKS:
                    queue.put(parse_sticks_packet(data))
                elif len(data) == BUTTONS_PACKET_LEN and cmd_id == DUML_CMD_BUTTONS:
                    update = parse_buttons_packet(data)
                    if update is not None:
                        queue.put(update)
        except serial.SerialException:
            if not shutdown.is_set():
                raise

        elapsed = time.perf_counter() - loop_start
        remaining = SERIAL_POLL_INTERVAL - elapsed
        if remaining > 0:
            time.sleep(remaining)


# ── Gamepad output ───────────────────────────────────────────────────────────

class GamepadOutput:
    """Manages the virtual Xbox 360 gamepad state and output.

    Accumulates SticksUpdate / ButtonsUpdate via apply(), then pushes
    the combined state to ViGEm on each push() call.
    """

    _MODE_BUTTONS = {
        "cine": vg.XUSB_BUTTON.XUSB_GAMEPAD_DPAD_LEFT,
        "normal": vg.XUSB_BUTTON.XUSB_GAMEPAD_DPAD_UP,
        "sport": vg.XUSB_BUTTON.XUSB_GAMEPAD_DPAD_RIGHT,
    }
    _ALL_DPAD_MODES = tuple(_MODE_BUTTONS.values())

    def __init__(self, mode_style: str) -> None:
        self._gp = vg.VX360Gamepad()
        self._gp.reset()
        self._mode_style = mode_style
        self._sticks = SticksUpdate(0, 0, 0, 0, 0, 0)
        self._buttons = ButtonsUpdate(False, False, False, False, "normal")
        self._prev_mode = "normal"
        self._pulse_until = 0.0

    def apply(self, update: ControllerUpdate) -> None:
        """Apply a parsed update to internal state."""
        if isinstance(update, SticksUpdate):
            self._sticks = update
        elif isinstance(update, ButtonsUpdate):
            if update.mode != self._prev_mode:
                self._pulse_until = time.time() + MODE_PULSE_DURATION
                self._prev_mode = update.mode
            self._buttons = update

    def push(self) -> None:
        """Send current state to the virtual gamepad driver."""
        s = self._sticks
        b = self._buttons

        self._gp.left_joystick(s.lh, s.lv)
        self._gp.right_joystick(s.rh, s.rv)
        self._gp.left_trigger(s.gimbal_lt)
        self._gp.right_trigger(s.gimbal_rt)

        self._set_button(vg.XUSB_BUTTON.XUSB_GAMEPAD_RIGHT_SHOULDER, b.camera_shoot)
        self._set_button(vg.XUSB_BUTTON.XUSB_GAMEPAD_LEFT_SHOULDER, b.fn)
        self._set_button(vg.XUSB_BUTTON.XUSB_GAMEPAD_Y, b.camera_swap)
        self._set_button(vg.XUSB_BUTTON.XUSB_GAMEPAD_BACK, b.rth)
        self._apply_mode_switch(b.mode)

        self._gp.update()

    def _set_button(self, button: int, pressed: bool) -> None:
        if pressed:
            self._gp.press_button(button)
        else:
            self._gp.release_button(button)

    def _apply_mode_switch(self, mode: str) -> None:
        now = time.time()

        if self._mode_style == "pulse":
            # Always clear all mode d-pad buttons first to avoid ghost presses
            for btn in self._ALL_DPAD_MODES:
                self._gp.release_button(btn)
            if now < self._pulse_until:
                self._gp.press_button(self._MODE_BUTTONS[mode])

        elif self._mode_style == "single":
            self._set_button(
                vg.XUSB_BUTTON.XUSB_GAMEPAD_DPAD_DOWN,
                now < self._pulse_until,
            )

        elif self._mode_style == "hold":
            for btn in self._ALL_DPAD_MODES:
                self._gp.release_button(btn)
            self._gp.press_button(self._MODE_BUTTONS[mode])

    @property
    def debug_state(self) -> tuple:
        """Return a hashable snapshot for change-detection in debug output."""
        s, b = self._sticks, self._buttons
        return (s.lh, s.lv, s.rh, s.rv, s.gimbal_lt, s.gimbal_rt,
                b.camera_shoot, b.fn, b.camera_swap, b.rth, b.mode)


# ── Config + UI ──────────────────────────────────────────────────────────────

def load_config() -> dict:
    try:
        with open(CONFIG_FILE, "r") as f:
            return json.load(f)
    except (FileNotFoundError, json.JSONDecodeError):
        return {}


def save_config(cfg: dict) -> None:
    with open(CONFIG_FILE, "w") as f:
        json.dump(cfg, f, indent=2)


def ask_mode_style(console: Console) -> str:
    """Arrow-key interactive selector for mode switch behavior. Remembers last choice."""
    cfg = load_config()
    last = cfg.get("mode_style", "pulse")

    options = ["pulse", "single", "hold"]
    labels = {
        "pulse":  "D-pad Left/Up/Right per mode    pulse on change",
        "single": "D-pad Down on any mode change    pulse",
        "hold":   "D-pad Left/Up/Right per mode    held while in mode",
    }
    descriptions = {
        "pulse":  "Sends a brief D-pad press matching the mode when you flip the switch",
        "single": "Sends a brief D-pad Down press on any mode change",
        "hold":   "Holds the D-pad direction as long as the switch is in that position",
    }

    selected = options.index(last) if last in options else 0

    def render(idx: int) -> None:
        console.clear()
        console.print()
        console.print(Panel(
            "[bold cyan]DJI RC-N3 Simulator Bridge[/bold cyan]",
            subtitle=f"v{VERSION}",
            style="bright_blue",
            width=60,
        ))
        console.print()
        console.print("  [bold yellow]Mode Switch Style[/bold yellow]")
        console.print("  [dim]Use [bold]Up/Down[/bold] arrows to select, "
                       "[bold]Enter[/bold] to confirm[/dim]\n")

        for i, opt in enumerate(options):
            if i == idx:
                console.print(f"  [bold green]> ({chr(9679)}) {labels[opt]}[/bold green]")
            else:
                console.print(f"  [dim]  ({chr(9675)}) {labels[opt]}[/dim]")

        console.print()
        console.print(f"  [italic bright_black]{descriptions[options[idx]]}[/italic bright_black]")
        console.print()

    render(selected)

    while True:
        key = msvcrt.getch()
        if key == b"\r":
            break
        if key in (b"\xe0", b"\x00"):
            arrow = msvcrt.getch()
            if arrow == b"H":
                selected = (selected - 1) % len(options)
                render(selected)
            elif arrow == b"P":
                selected = (selected + 1) % len(options)
                render(selected)

    chosen = options[selected]
    cfg["mode_style"] = chosen
    save_config(cfg)
    return chosen


def open_serial(console: Console, fallback_port: str | None) -> serial.Serial:
    """Auto-detect DJI USB VCOM port, fall back to explicit port argument."""
    ports = serial.tools.list_ports.comports(True)
    for port in ports:
        try:
            console.print(f"    [dim]{port.name}: {port.description}[/dim]")
            if "For Protocol" in port.description:
                return serial.Serial(port=port.name, baudrate=115200, timeout=SERIAL_TIMEOUT)
        except (OSError, serial.SerialException):
            pass

    if fallback_port:
        console.print(f"    [yellow]No auto-detect, using fallback: {fallback_port}[/yellow]")
        return serial.Serial(port=fallback_port, baudrate=115200, timeout=SERIAL_TIMEOUT)

    raise serial.SerialException(
        "No DJI USB VCOM port found. Connect the RC-N3 or use -p COM<n>"
    )


def print_mapping_table(console: Console, mode_style: str) -> None:
    table = Table(
        title="Gamepad Mapping", style="bright_blue", title_style="bold cyan",
        show_header=True, header_style="bold white", width=58, pad_edge=True,
    )
    table.add_column("RC-N3 Control", style="white", min_width=22)
    table.add_column("Xbox 360", style="green", min_width=28)

    table.add_row("Left stick", "Left joystick")
    table.add_row("Right stick", "Right joystick")
    table.add_row("Gimbal scroll down", "Left Trigger (LT)")
    table.add_row("Gimbal scroll up", "Right Trigger (RT)")
    table.add_row("Camera shoot btn", "Right Bumper (RB)")
    table.add_row("FN button", "Left Bumper (LB)")
    table.add_row("Camera swap btn", "Y button")
    table.add_row("RTH button", "Back button")

    if mode_style == "pulse":
        table.add_row("Mode: Cinematic", "D-pad Left [dim](pulse)[/dim]")
        table.add_row("Mode: Normal", "D-pad Up [dim](pulse)[/dim]")
        table.add_row("Mode: Sport", "D-pad Right [dim](pulse)[/dim]")
    elif mode_style == "single":
        table.add_row("Mode: any change", "D-pad Down [dim](pulse)[/dim]")
    elif mode_style == "hold":
        table.add_row("Mode: Cinematic", "D-pad Left [dim](held)[/dim]")
        table.add_row("Mode: Normal", "D-pad Up [dim](held)[/dim]")
        table.add_row("Mode: Sport", "D-pad Right [dim](held)[/dim]")

    console.print(table)


# ── Entry point ──────────────────────────────────────────────────────────────

def main() -> None:
    console = Console()

    parser = argparse.ArgumentParser(description="DJI RC-N3 Simulator Bridge")
    parser.add_argument("-p", "--port", help="RC Serial Port (fallback if auto-detect fails)")
    parser.add_argument("-d", "--debug", action="store_true", help="Print parsed state on changes")
    args = parser.parse_args()

    # Windows high-resolution timer (1ms instead of default ~15.6ms)
    try:
        ctypes.windll.winmm.timeBeginPeriod(1)
    except Exception:
        pass

    mode_style = ask_mode_style(console)

    # Banner
    console.clear()
    console.print()
    console.print(Panel(
        "[bold cyan]DJI RC-N3 Simulator Bridge[/bold cyan]",
        subtitle=f"v{VERSION}",
        style="bright_blue",
        width=60,
    ))
    console.print()

    # Serial connection
    console.print("  [yellow]Scanning serial ports...[/yellow]")
    try:
        port = open_serial(console, args.port)
        console.print(f"  [green]Connected to {port.name}[/green]\n")
    except serial.SerialException as e:
        console.print(f"  [bold red]Error: {e}[/bold red]")
        return

    conn = DumlConnection(port)
    gp_out = GamepadOutput(mode_style)
    time.sleep(1)  # let ViGEm driver initialise

    print_mapping_table(console, mode_style)
    console.print()
    console.print("  [bold green]RC-N3 emulation started.[/bold green]")
    console.print("  [dim]Press Ctrl+C to stop.[/dim]\n")

    # Enable simulator mode on the RC
    conn.send(DUML_CMD_SIM_MODE, b"\x01")

    # Thread communication
    update_queue: SimpleQueue[ControllerUpdate] = SimpleQueue()
    shutdown = Event()

    reader = Thread(
        target=serial_reader,
        args=(conn, update_queue, shutdown),
        daemon=True,
        name="serial-reader",
    )
    reader.start()

    last_debug: tuple | None = None

    try:
        while True:
            loop_start = time.perf_counter()

            # Drain all queued updates into gamepad state
            while True:
                try:
                    gp_out.apply(update_queue.get_nowait())
                except Empty:
                    break

            gp_out.push()

            if args.debug:
                state = gp_out.debug_state
                if state != last_debug:
                    last_debug = state
                    print(
                        f"\rSticks LH:{state[0]:+6d} LV:{state[1]:+6d} "
                        f"RH:{state[2]:+6d} RV:{state[3]:+6d} "
                        f"| LT:{state[4]:3d} RT:{state[5]:3d} "
                        f"| Mode:{state[10]:6s} "
                        f"| shoot:{state[6]} fn:{state[7]} "
                        f"swap:{state[8]} rth:{state[9]}",
                        end="",
                    )

            # Maintain steady ~100Hz gamepad output
            elapsed = time.perf_counter() - loop_start
            remaining = GAMEPAD_UPDATE_INTERVAL - elapsed
            if remaining > 0:
                time.sleep(remaining)

    except serial.SerialException as e:
        console.print(f"\n  [bold red]Serial error: {e}[/bold red]")
    except KeyboardInterrupt:
        pass
    finally:
        shutdown.set()
        reader.join(timeout=1.0)
        conn.close()
        try:
            ctypes.windll.winmm.timeEndPeriod(1)
        except Exception:
            pass

    console.print("\n  [yellow]Stopping.[/yellow]")


if __name__ == "__main__":
    main()
