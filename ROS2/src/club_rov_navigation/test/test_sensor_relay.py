"""The Windows return-telemetry listener may close during Unity reloads."""
import importlib.util
from pathlib import Path
import unittest
from unittest.mock import Mock, patch


class SensorRelayRecoveryTest(unittest.TestCase):
    def test_udp_reset_does_not_stop_sensor_forwarding(self):
        path = Path(__file__).resolve().parents[3] / 'tools' / 'relay-unity-sensors.py'
        spec = importlib.util.spec_from_file_location('sensor_relay_under_test', path)
        relay = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(relay)
        udp, tcp = Mock(), Mock()
        udp.recvfrom.side_effect = [ConnectionResetError(10054, 'listener closed'),
                                   (b'{"schema":1}', ('127.0.0.1', 12345)),
                                   BlockingIOError()]
        with patch.object(relay.socket, 'socket', return_value=udp), \
                patch.object(relay.socket, 'create_connection', return_value=tcp), \
                patch.object(relay.select, 'select', side_effect=[([udp], [], []), KeyboardInterrupt()]), \
                patch('sys.argv', [str(path)]):
            relay.main()
        tcp.sendall.assert_called_once_with(b'{"schema":1}\n')
        tcp.close.assert_called_once()
        udp.close.assert_called_once()
