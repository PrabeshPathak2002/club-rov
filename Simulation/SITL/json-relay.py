"""Loopback-only JSON UDP tunnel through WSL's TCP localhost forwarding.

Run --server in WSL, --client on Windows. The server uses the installed pymavlink
environment; the Windows client uses only standard Python. No firewall changes
or network-facing relay listeners are required. Ctrl+C stops either.
"""
import argparse
import select
import socket
import struct
import json
import time


def relay(server):
    udp = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    udp.bind(("127.0.0.1", 9002 if server else 0))
    # A stopped/recompiling Unity listener can generate ICMP port-unreachable.
    # Windows otherwise surfaces that as a fatal reset on the next UDP receive.
    if hasattr(socket, "SIO_UDP_CONNRESET"):
        udp.ioctl(socket.SIO_UDP_CONNRESET, False)
    listener = None
    telemetry = None
    if server:
        from pymavlink import mavutil
        telemetry = mavutil.mavlink_connection('udpin:127.0.0.1:14552')
        listener = socket.socket()
        listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        listener.bind(("127.0.0.1", 9003))
        listener.listen(1)
        print("JSON relay listening on WSL localhost:9003", flush=True)
        stream, _ = listener.accept()
    else:
        deadline=time.monotonic()+10
        while True:
            try:
                stream = socket.create_connection(("127.0.0.1", 9003), timeout=1)
                break
            except (ConnectionRefusedError,TimeoutError):
                if time.monotonic()>=deadline:
                    raise
                time.sleep(0.25)
        stream.settimeout(None)
    stream.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
    peer = None if server else ("127.0.0.1", 9002)
    pending = bytearray()
    logged_udp = logged_tcp = False
    print("JSON relay connected", flush=True)
    try:
        while True:
            watched = [udp, stream] + ([telemetry.port] if telemetry is not None else [])
            readable, _, _ = select.select(watched, [], [], 1)
            if telemetry is not None and telemetry.port in readable:
                for _ in range(100):
                    message = telemetry.recv_match(blocking=False)
                    if message is None:
                        break
                    if message.get_type() == 'HEARTBEAT' and message.get_srcSystem() == 42:
                        data = json.dumps(dict(sysid=42, armed=bool(message.base_mode & 128), mode=message.custom_mode)).encode()
                        stream.sendall(struct.pack('<I', len(data)) + data)
            if udp in readable:
                try:
                    data, source = udp.recvfrom(65535)
                except ConnectionResetError:
                    continue  # Unity can stop/restart its listener between sessions.
                if not logged_udp:
                    print(f"First UDP: {len(data)} bytes from {source}, header {data[:8].hex()}", flush=True)
                    logged_udp = True
                if source[0] != "127.0.0.1":
                    continue
                if server:
                    peer = source
                elif source != peer:
                    continue
                stream.sendall(struct.pack("<I", len(data)) + data)
            if stream in readable:
                chunk = stream.recv(65535)
                if not chunk:
                    break
                pending.extend(chunk)
                while len(pending) >= 4:
                    size = struct.unpack_from("<I", pending)[0]
                    if not 1 <= size <= 4096:
                        raise ValueError("Invalid relay frame length")
                    if len(pending) < 4 + size:
                        break
                    data = pending[4:4 + size]
                    del pending[:4 + size]
                    if not logged_tcp:
                        print(f"First tunneled packet: {size} bytes, header {data[:8].hex()}", flush=True)
                        logged_tcp = True
                    if not server and data.startswith(b'{'):
                        udp.sendto(data, ('127.0.0.1', 9004))
                    elif peer is not None:
                        udp.sendto(data, peer)
    finally:
        stream.close()
        udp.close()
        if listener:
            listener.close()
        if telemetry is not None:
            telemetry.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--server", action="store_true")
    group.add_argument("--client", action="store_true")
    args = parser.parse_args()
    try:
        relay(args.server)
    except KeyboardInterrupt:
        pass
