"""Loopback-only JSON UDP tunnel through WSL's TCP localhost forwarding.

Run --server in WSL, --client on Windows. No firewall changes, third-party
Python packages, or network-facing listeners are required. Ctrl+C stops either.
"""
import argparse
import select
import socket
import struct


def relay(server):
    udp = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    udp.bind(("127.0.0.1", 9002 if server else 0))
    # A stopped/recompiling Unity listener can generate ICMP port-unreachable.
    # Windows otherwise surfaces that as a fatal reset on the next UDP receive.
    if hasattr(socket, "SIO_UDP_CONNRESET"):
        udp.ioctl(socket.SIO_UDP_CONNRESET, False)
    listener = None
    if server:
        listener = socket.socket()
        listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        listener.bind(("127.0.0.1", 9003))
        listener.listen(1)
        print("JSON relay listening on WSL localhost:9003", flush=True)
        stream, _ = listener.accept()
    else:
        stream = socket.create_connection(("127.0.0.1", 9003), timeout=5)
        stream.settimeout(None)
    stream.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
    peer = None if server else ("127.0.0.1", 9002)
    pending = bytearray()
    logged_udp = logged_tcp = False
    print("JSON relay connected", flush=True)
    try:
        while True:
            readable, _, _ = select.select([udp, stream], [], [], 1)
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
                    if peer is not None:
                        udp.sendto(data, peer)
    finally:
        stream.close()
        udp.close()
        if listener:
            listener.close()


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
