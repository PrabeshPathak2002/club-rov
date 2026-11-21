#!/usr/bin/env python3
"""Windows loopback UDP 9010 -> WSL localhost TCP 9011, without replay queues."""
import argparse
import select
import socket
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--udp-port', type=int, default=9010)
    parser.add_argument('--tcp-port', type=int, default=9011)
    args = parser.parse_args()
    receiver = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    receiver.bind(('127.0.0.1', args.udp_port))
    if hasattr(socket, 'SIO_UDP_CONNRESET'):
        receiver.ioctl(socket.SIO_UDP_CONNRESET, False)
    receiver.setblocking(False)
    client = None
    pending = bytearray()
    retry_at = 0
    print(f'Unity UDP {args.udp_port} -> ROS TCP {args.tcp_port}. Ctrl+C stops the relay.', flush=True)
    try:
        while True:
            if client is None and time.monotonic() >= retry_at:
                try:
                    client = socket.create_connection(('127.0.0.1', args.tcp_port), timeout=0.1)
                    client.settimeout(0.05)
                    client.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
                    print('Connected to ROS sensor bridge.', flush=True)
                except OSError:
                    client = None
                    retry_at = time.monotonic()+1
            readable, _, _ = select.select([receiver]+([client] if client else []), [], [], 0.05)
            if client is not None and client in readable:
                try:
                    chunk = client.recv(8192)
                    if not chunk:
                        raise ConnectionResetError()
                    pending.extend(chunk)
                    while b'\n' in pending:
                        index = pending.index(b'\n')
                        if index > 8192:
                            raise ValueError('Oversize return telemetry')
                        receiver.sendto(bytes(pending[:index]), ('127.0.0.1', 9012))
                        del pending[:index+1]
                    if len(pending) > 8192:
                        raise ValueError('Oversize return telemetry')
                except (OSError, ValueError):
                    client.close()
                    client = None
                    pending.clear()
                    retry_at = time.monotonic()+1
            for _ in range(64 if receiver in readable else 0):
                try:
                    data, _ = receiver.recvfrom(8193)
                except BlockingIOError:
                    break
                except ConnectionResetError:
                    # Unity's return-telemetry listener can disappear during a
                    # Play Mode restart. Windows may report its ICMP response
                    # here; keep the sensor relay alive for the new listener.
                    continue
                if len(data) > 8192 or b'\n' in data or not client:
                    continue
                try:
                    client.sendall(data+b'\n')
                except OSError:
                    client.close()
                    client = None
                    pending.clear()
                    retry_at = time.monotonic()+1
    except KeyboardInterrupt:
        pass
    finally:
        if client:
            client.close()
        receiver.close()


if __name__ == '__main__':
    main()
