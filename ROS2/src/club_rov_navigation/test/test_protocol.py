import json
import socket
import time
import pytest
from club_rov_navigation.protocol import decode, compensate_lever_arm, SequenceGuard, DEFAULT_OFFSET
from club_rov_navigation.transport import Receiver


def packet(**changes):
    p = dict(schema=1, session='a'*32, seq=1, stamp_ns=10_000_000,
             gyro=[0,0,0], accel=[0,0,9.81], dvl_offset=list(DEFAULT_OFFSET),
             gyro_variance=4e-6, accel_variance=4e-4, depth=0, depth_variance=2.5e-5,
             has_dvl=True, valid=True, has_truth=False, beams=3, altitude=1, velocity_variance=1e-4,
             velocity=[1,0,0], reason='bottom_lock', truth=None)
    p.update(changes)
    return p


def test_velocity_and_invalid_reports():
    assert decode(json.dumps(packet()))['velocity'] == [1,0,0]
    assert not decode(json.dumps(packet(valid=False, beams=2, reason='no_bottom_lock')))['valid']


@pytest.mark.parametrize('changes', [dict(gyro=[float('nan'),0,0]), dict(accel=[0,1]),
    dict(stamp_ns=-1), dict(seq=True), dict(gyro_variance=0), dict(valid=True, beams=2),
    dict(dvl_offset=[0,0,0]), dict(session='bad'), dict(has_truth=True, truth={'position':[0,0,0],
        'velocity':[0,0,0], 'angular_velocity':[0,0,0], 'orientation':[0,0,0,2]}), dict(valid='true')])
def test_reject_corrupt_measurements(changes):
    with pytest.raises(ValueError):
        decode(json.dumps(packet(**changes)))


def test_lever_arm_rotation_does_not_become_translation():
    assert compensate_lever_arm([0,0.36,0], [2,0,0], [0,0,-0.18]) == pytest.approx([0,0,0])


def test_unity_null_truth_object_is_not_a_measurement():
    p = decode(json.dumps(packet(truth={'position':[], 'orientation':[], 'velocity':[], 'angular_velocity':[]})))
    assert p['truth'] is None


def test_sequence_and_session_are_fail_closed():
    guard = SequenceGuard()
    guard.accept(packet())
    assert guard.fresh()
    for p in [packet(), packet(seq=2,stamp_ns=1), packet(session='b'*32,seq=2,stamp_ns=20_000_000)]:
        with pytest.raises(ValueError): guard.accept(p)
    guard.accept(packet(seq=2,stamp_ns=20_000_000))
    guard.last_received = time.monotonic()-1
    assert not guard.fresh()


def test_tcp_fragmentation_bounds_and_reconnect():
    receiver = Receiver(0)
    port = receiver.server.getsockname()[1]
    try:
        with socket.create_connection(('127.0.0.1',port)) as sender:
            receiver.poll()
            sender.sendall(b'{"schema":')
            assert receiver.poll() == []
            sender.sendall(b'1}\n{}\n')
            assert receiver.poll() == [b'{"schema":1}',b'{}']
            assert receiver.send_latest(b'{"kind":"dvl_track"}')
            assert sender.recv(256) == b'{"kind":"dvl_track"}\n'
            assert not receiver.send_latest(b'bad\nframe')
            sender.sendall(b'x'*8193)
            for _ in range(4): receiver.poll()
            assert receiver.client is None
        with socket.create_connection(('127.0.0.1',port)) as sender:
            sender.sendall(b'{}\n')
            assert receiver.poll() == [b'{}']
    finally:
        receiver.close()
