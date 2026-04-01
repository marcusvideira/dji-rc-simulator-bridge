"""
Button capture tool v2 for DJI RC-N3.
Reads ALL packet types and tries multiple DUML commands to find button data.
"""
import struct
import time
import serial
import serial.tools.list_ports
from collections import Counter

def calc_checksum(packet, plength):
    crc = [0x0000, 0x1189, 0x2312, 0x329b, 0x4624, 0x57ad, 0x6536, 0x74bf,
    0x8c48, 0x9dc1, 0xaf5a, 0xbed3, 0xca6c, 0xdbe5, 0xe97e, 0xf8f7,
    0x1081, 0x0108, 0x3393, 0x221a, 0x56a5, 0x472c, 0x75b7, 0x643e,
    0x9cc9, 0x8d40, 0xbfdb, 0xae52, 0xdaed, 0xcb64, 0xf9ff, 0xe876,
    0x2102, 0x308b, 0x0210, 0x1399, 0x6726, 0x76af, 0x4434, 0x55bd,
    0xad4a, 0xbcc3, 0x8e58, 0x9fd1, 0xeb6e, 0xfae7, 0xc87c, 0xd9f5,
    0x3183, 0x200a, 0x1291, 0x0318, 0x77a7, 0x662e, 0x54b5, 0x453c,
    0xbdcb, 0xac42, 0x9ed9, 0x8f50, 0xfbef, 0xea66, 0xd8fd, 0xc974,
    0x4204, 0x538d, 0x6116, 0x709f, 0x0420, 0x15a9, 0x2732, 0x36bb,
    0xce4c, 0xdfc5, 0xed5e, 0xfcd7, 0x8868, 0x99e1, 0xab7a, 0xbaf3,
    0x5285, 0x430c, 0x7197, 0x601e, 0x14a1, 0x0528, 0x37b3, 0x263a,
    0xdecd, 0xcf44, 0xfddf, 0xec56, 0x98e9, 0x8960, 0xbbfb, 0xaa72,
    0x6306, 0x728f, 0x4014, 0x519d, 0x2522, 0x34ab, 0x0630, 0x17b9,
    0xef4e, 0xfec7, 0xcc5c, 0xddd5, 0xa96a, 0xb8e3, 0x8a78, 0x9bf1,
    0x7387, 0x620e, 0x5095, 0x411c, 0x35a3, 0x242a, 0x16b1, 0x0738,
    0xffcf, 0xee46, 0xdcdd, 0xcd54, 0xb9eb, 0xa862, 0x9af9, 0x8b70,
    0x8408, 0x9581, 0xa71a, 0xb693, 0xc22c, 0xd3a5, 0xe13e, 0xf0b7,
    0x0840, 0x19c9, 0x2b52, 0x3adb, 0x4e64, 0x5fed, 0x6d76, 0x7cff,
    0x9489, 0x8500, 0xb79b, 0xa612, 0xd2ad, 0xc324, 0xf1bf, 0xe036,
    0x18c1, 0x0948, 0x3bd3, 0x2a5a, 0x5ee5, 0x4f6c, 0x7df7, 0x6c7e,
    0xa50a, 0xb483, 0x8618, 0x9791, 0xe32e, 0xf2a7, 0xc03c, 0xd1b5,
    0x2942, 0x38cb, 0x0a50, 0x1bd9, 0x6f66, 0x7eef, 0x4c74, 0x5dfd,
    0xb58b, 0xa402, 0x9699, 0x8710, 0xf3af, 0xe226, 0xd0bd, 0xc134,
    0x39c3, 0x284a, 0x1ad1, 0x0b58, 0x7fe7, 0x6e6e, 0x5cf5, 0x4d7c,
    0xc60c, 0xd785, 0xe51e, 0xf497, 0x8028, 0x91a1, 0xa33a, 0xb2b3,
    0x4a44, 0x5bcd, 0x6956, 0x78df, 0x0c60, 0x1de9, 0x2f72, 0x3efb,
    0xd68d, 0xc704, 0xf59f, 0xe416, 0x90a9, 0x8120, 0xb3bb, 0xa232,
    0x5ac5, 0x4b4c, 0x79d7, 0x685e, 0x1ce1, 0x0d68, 0x3ff3, 0x2e7a,
    0xe70e, 0xf687, 0xc41c, 0xd595, 0xa12a, 0xb0a3, 0x8238, 0x93b1,
    0x6b46, 0x7acf, 0x4854, 0x59dd, 0x2d62, 0x3ceb, 0x0e70, 0x1ff9,
    0xf78f, 0xe606, 0xd49d, 0xc514, 0xb1ab, 0xa022, 0x92b9, 0x8330,
    0x7bc7, 0x6a4e, 0x58d5, 0x495c, 0x3de3, 0x2c6a, 0x1ef1, 0x0f78]
    v = 0x3692
    for i in range(0, plength):
        vv = v >> 8
        v = vv ^ crc[((packet[i] ^ v) & 0xFF)]
    return v

def calc_pkt55_hdr_checksum(seed, packet, plength):
    arr_2A103 = [0x00,0x5E,0xBC,0xE2,0x61,0x3F,0xDD,0x83,0xC2,0x9C,0x7E,0x20,0xA3,0xFD,0x1F,0x41,
        0x9D,0xC3,0x21,0x7F,0xFC,0xA2,0x40,0x1E,0x5F,0x01,0xE3,0xBD,0x3E,0x60,0x82,0xDC,
        0x23,0x7D,0x9F,0xC1,0x42,0x1C,0xFE,0xA0,0xE1,0xBF,0x5D,0x03,0x80,0xDE,0x3C,0x62,
        0xBE,0xE0,0x02,0x5C,0xDF,0x81,0x63,0x3D,0x7C,0x22,0xC0,0x9E,0x1D,0x43,0xA1,0xFF,
        0x46,0x18,0xFA,0xA4,0x27,0x79,0x9B,0xC5,0x84,0xDA,0x38,0x66,0xE5,0xBB,0x59,0x07,
        0xDB,0x85,0x67,0x39,0xBA,0xE4,0x06,0x58,0x19,0x47,0xA5,0xFB,0x78,0x26,0xC4,0x9A,
        0x65,0x3B,0xD9,0x87,0x04,0x5A,0xB8,0xE6,0xA7,0xF9,0x1B,0x45,0xC6,0x98,0x7A,0x24,
        0xF8,0xA6,0x44,0x1A,0x99,0xC7,0x25,0x7B,0x3A,0x64,0x86,0xD8,0x5B,0x05,0xE7,0xB9,
        0x8C,0xD2,0x30,0x6E,0xED,0xB3,0x51,0x0F,0x4E,0x10,0xF2,0xAC,0x2F,0x71,0x93,0xCD,
        0x11,0x4F,0xAD,0xF3,0x70,0x2E,0xCC,0x92,0xD3,0x8D,0x6F,0x31,0xB2,0xEC,0x0E,0x50,
        0xAF,0xF1,0x13,0x4D,0xCE,0x90,0x72,0x2C,0x6D,0x33,0xD1,0x8F,0x0C,0x52,0xB0,0xEE,
        0x32,0x6C,0x8E,0xD0,0x53,0x0D,0xEF,0xB1,0xF0,0xAE,0x4C,0x12,0x91,0xCF,0x2D,0x73,
        0xCA,0x94,0x76,0x28,0xAB,0xF5,0x17,0x49,0x08,0x56,0xB4,0xEA,0x69,0x37,0xD5,0x8B,
        0x57,0x09,0xEB,0xB5,0x36,0x68,0x8A,0xD4,0x95,0xCB,0x29,0x77,0xF4,0xAA,0x48,0x16,
        0xE9,0xB7,0x55,0x0B,0x88,0xD6,0x34,0x6A,0x2B,0x75,0x97,0xC9,0x4A,0x14,0xF6,0xA8,
        0x74,0x2A,0xC8,0x96,0x15,0x4B,0xA9,0xF7,0xB6,0xE8,0x0A,0x54,0xD7,0x89,0x6B,0x35]
    chksum = seed
    for i in range(0, plength):
        chksum = arr_2A103[((packet[i] ^ chksum) & 0xFF)]
    return chksum

def send_duml(s, source, target, cmd_type, cmd_set, cmd_id, payload=None):
    packet = bytearray.fromhex(u'55')
    length = 13
    if payload is not None:
        length = length + len(payload)
    packet += struct.pack('B', length & 0xff)
    packet += struct.pack('B', (length >> 8) | 0x4)
    hdr_crc = calc_pkt55_hdr_checksum(0x77, packet, 3)
    packet += struct.pack('B', hdr_crc)
    packet += struct.pack('B', source)
    packet += struct.pack('B', target)
    packet += struct.pack('<H', 0x34eb)
    packet += struct.pack('B', cmd_type)
    packet += struct.pack('B', cmd_set)
    packet += struct.pack('B', cmd_id)
    if payload is not None:
        packet += payload
    crc = calc_checksum(packet, len(packet))
    packet += struct.pack('<H', crc)
    s.write(packet)

def read_packet_nonblocking(s):
    """Read a DUML packet if data is available."""
    if s.in_waiting == 0:
        return None
    b = s.read(1)
    if b != b'\x55':
        return None
    buffer = bytearray(b)
    ph = s.read(2)
    buffer.extend(ph)
    ph_val = struct.unpack('<H', ph)[0]
    pl = ph_val & 0x3FF
    pc = s.read(1)
    buffer.extend(pc)
    remaining = pl - 4
    if remaining > 0:
        pd = s.read(remaining)
        buffer.extend(pd)
    return bytes(buffer)

def packet_key(data):
    """Create a key for a packet: (length, cmd_set, cmd_id, payload_without_seq_crc)."""
    if len(data) < 13:
        return (len(data), 0, 0, data)
    cmd_set = data[9]
    cmd_id = data[10]
    # payload = bytes between header and CRC, excluding sequence number (bytes 6-7)
    payload = data[4:6] + data[8:-2]  # source+target + cmd_type+cmd_set+cmd_id+payload
    return (len(data), cmd_set, cmd_id, payload)

def describe_packet(data):
    """Human-readable description of a packet."""
    if len(data) < 11:
        return f"len={len(data)} raw={data.hex()}"
    src = data[4]
    tgt = data[5]
    cmd_type = data[8]
    cmd_set = data[9]
    cmd_id = data[10]
    payload = data[11:-2] if len(data) > 13 else b''
    return (f"len={len(data)} src=0x{src:02x} tgt=0x{tgt:02x} "
            f"cmd_type=0x{cmd_type:02x} cmd_set=0x{cmd_set:02x} cmd_id=0x{cmd_id:02x} "
            f"payload=[{' '.join(f'{x:02x}' for x in payload)}]")

def open_serial():
    ports = serial.tools.list_ports.comports(True)
    for port in ports:
        try:
            if "For Protocol" in port.description:
                return serial.Serial(port=port.name, baudrate=115200, timeout=0.1)
        except (OSError, serial.SerialException):
            pass
    raise serial.SerialException("No DJI USB VCOM port found")


# ── DUML commands to try ──
# Each: (description, cmd_set, cmd_id, payload_hex)
EXTRA_COMMANDS = [
    ("RC button state (0x06/0x05)", 0x06, 0x05, ''),
    ("RC button state (0x06/0x06)", 0x06, 0x06, ''),
    ("RC switch state (0x06/0x07)", 0x06, 0x07, ''),
    ("RC push config (0x06/0x02)", 0x06, 0x02, ''),
    ("RC info (0x06/0x00)", 0x06, 0x00, ''),
    ("RC all params (0x06/0x03)", 0x06, 0x03, ''),
    ("RC special fn (0x06/0x08)", 0x06, 0x08, ''),
    ("RC custom keys (0x06/0x09)", 0x06, 0x09, ''),
    ("RC key event (0x06/0x0a)", 0x06, 0x0a, ''),
    ("RC key event (0x06/0x20)", 0x06, 0x20, ''),
    ("RC key event (0x06/0x25)", 0x06, 0x25, ''),
    ("RC key event (0x06/0x26)", 0x06, 0x26, ''),
    ("RC key event (0x06/0x27)", 0x06, 0x27, ''),
    ("RC key event (0x06/0x28)", 0x06, 0x28, ''),
    ("RC key event (0x06/0x29)", 0x06, 0x29, ''),
    ("RC key event (0x06/0x2a)", 0x06, 0x2a, ''),
    ("RC key event (0x06/0x2b)", 0x06, 0x2b, ''),
]

print("=" * 60)
print("DJI RC-N3 Button Capture Tool v2")
print("=" * 60)

s = open_serial()
print(f"Connected to {s.name}\n")

# Enable simulator mode
send_duml(s, 0x0a, 0x06, 0x40, 0x06, 0x24, bytearray.fromhex('01'))
time.sleep(0.5)

# Drain any buffered data
while s.in_waiting > 0:
    s.read(s.in_waiting)

# ── Phase 1: Discover all packet types ──
print("Phase 1: Discovering all packet types (5 seconds)...")
print("  Sending channel request + extra DUML commands...\n")

all_packets = {}
end_time = time.time() + 5

while time.time() < end_time:
    # Send channel request
    send_duml(s, 0x0a, 0x06, 0x40, 0x06, 0x01, bytearray.fromhex(''))
    # Also send extra commands to probe for button data
    for desc, cmd_set, cmd_id, payload_hex in EXTRA_COMMANDS:
        send_duml(s, 0x0a, 0x06, 0x40, cmd_set, cmd_id, bytearray.fromhex(payload_hex))

    # Read ALL available packets
    time.sleep(0.05)
    while True:
        pkt = read_packet_nonblocking(s)
        if pkt is None:
            break
        key = (len(pkt), pkt[9] if len(pkt) > 9 else 0, pkt[10] if len(pkt) > 10 else 0)
        if key not in all_packets:
            all_packets[key] = pkt
            print(f"  NEW packet type: {describe_packet(pkt)}")

print(f"\nFound {len(all_packets)} unique packet types.\n")

# ── Phase 2: Button capture ──
BUTTONS_TO_TEST = [
    "IDLE (don't press anything)",
    "Camera SHOOT button (right side) - HOLD",
    "FN button (small, front left) - HOLD",
    "Camera SWAP button (top right front) - HOLD",
    "RTH button (Return to Home) - HOLD",
    "Mode switch -> CINE (leftmost position)",
    "Mode switch -> NORMAL (center position)",
    "Mode switch -> SPORT (rightmost position)",
]

results = {}

for step, button_name in enumerate(BUTTONS_TO_TEST):
    input(f"\nStep {step + 1}/{len(BUTTONS_TO_TEST)}: '{button_name}' then press ENTER...")
    print(f"  Capturing 3 seconds...")

    # Drain buffer
    while s.in_waiting > 0:
        s.read(s.in_waiting)

    # Collect ALL packets for 3 seconds
    captured = {}  # key: (len, cmd_set, cmd_id) -> list of payloads
    end_time = time.time() + 3

    while time.time() < end_time:
        send_duml(s, 0x0a, 0x06, 0x40, 0x06, 0x01, bytearray.fromhex(''))
        for desc, cmd_set, cmd_id, payload_hex in EXTRA_COMMANDS:
            send_duml(s, 0x0a, 0x06, 0x40, cmd_set, cmd_id, bytearray.fromhex(payload_hex))
        time.sleep(0.05)
        while True:
            pkt = read_packet_nonblocking(s)
            if pkt is None:
                break
            if len(pkt) < 13:
                continue
            key = (len(pkt), pkt[9], pkt[10])
            if key not in captured:
                captured[key] = []
            # Store payload only (strip header seq and CRC)
            captured[key].append(pkt[11:-2])

    # For each packet type, find most common payload
    step_result = {}
    for key, payloads in captured.items():
        counter = Counter(payloads)
        most_common = counter.most_common(1)[0][0]
        step_result[key] = most_common
        plen, cs, ci = key
        labeled = ' '.join(f'{x:02x}' for x in most_common)
        print(f"  pkt(len={plen}, set=0x{cs:02x}, id=0x{ci:02x}): [{labeled}] ({len(payloads)} samples)")

    results[button_name] = step_result

# ── Analysis ──
print("\n" + "=" * 60)
print("ANALYSIS: Comparing each button to IDLE baseline")
print("=" * 60)

idle_name = BUTTONS_TO_TEST[0]
idle_result = results[idle_name]

for button_name in BUTTONS_TO_TEST[1:]:
    btn_result = results[button_name]
    print(f"\n>> {button_name}:")

    found_any = False
    # Check all packet types
    all_keys = set(list(idle_result.keys()) + list(btn_result.keys()))
    for key in sorted(all_keys):
        plen, cs, ci = key
        idle_payload = idle_result.get(key)
        btn_payload = btn_result.get(key)

        if idle_payload is None:
            print(f"   NEW packet type (len={plen}, set=0x{cs:02x}, id=0x{ci:02x}): [{' '.join(f'{x:02x}' for x in btn_payload)}]")
            found_any = True
            continue
        if btn_payload is None:
            print(f"   MISSING packet type (len={plen}, set=0x{cs:02x}, id=0x{ci:02x})")
            found_any = True
            continue

        diffs = []
        for i in range(min(len(idle_payload), len(btn_payload))):
            if idle_payload[i] != btn_payload[i]:
                diffs.append(f"byte[{i}]: {idle_payload[i]:02x}->{btn_payload[i]:02x}")

        if diffs:
            print(f"   pkt(len={plen}, set=0x{cs:02x}, id=0x{ci:02x}): {', '.join(diffs)}")
            found_any = True

    if not found_any:
        print(f"   NO CHANGES in any packet type")

print("\n" + "=" * 60)
print("DONE")
print("=" * 60)
