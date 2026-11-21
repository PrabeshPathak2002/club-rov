"""Bounded newline-delimited TCP receiver for the Windows-to-WSL relay."""
import socket
from .protocol import MAX_PACKET


class Receiver:
    def __init__(self, port=9011):
        self.server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.server.bind(('127.0.0.1', port))
        self.server.listen(1)
        self.server.setblocking(False)
        self.client = None
        self.buffer = bytearray()

    def disconnect(self):
        if self.client:
            self.client.close()
        self.client = None
        self.buffer.clear()

    def poll(self):
        if self.client is None:
            try:
                self.client, _ = self.server.accept()
                self.client.setblocking(False)
                self.client.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
            except BlockingIOError:
                return []
        messages = []
        # Bound work per ROS callback; a malformed client cannot grow the buffer indefinitely.
        for _ in range(16):
            try:
                data = self.client.recv(4096)
            except BlockingIOError:
                break
            except (ConnectionResetError, OSError):
                self.disconnect()
                break
            if not data:
                self.disconnect()
                break
            self.buffer.extend(data)
            while b'\n' in self.buffer:
                index = self.buffer.index(b'\n')
                if index > MAX_PACKET:
                    self.disconnect()
                    return messages
                messages.append(bytes(self.buffer[:index]))
                del self.buffer[:index+1]
            if len(self.buffer) > MAX_PACKET:
                self.disconnect()
                return messages
        return messages

    def close(self):
        self.disconnect()
        self.server.close()

    def send_latest(self, payload):
        """Best-effort return telemetry; disconnect on partial writes, never queue stale data."""
        if self.client is None or len(payload) > MAX_PACKET or b'\n' in payload:
            return False
        data = payload+b'\n'
        try:
            if self.client.send(data) == len(data):
                return True
        except OSError:
            pass
        self.disconnect()
        return False
