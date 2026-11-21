"""Strict simulation wire contract, independent of ROS for offline validation."""
import json
import math
import time

MAX_PACKET = 8192
DEFAULT_OFFSET = (-0.000857, 0.0, -0.18)


def number(value, name):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        raise ValueError(f'{name} must be finite')
    return float(value)


def vector(value, size, name):
    if not isinstance(value, list) or len(value) != size:
        raise ValueError(f'{name} must have {size} elements')
    return [number(x, name) for x in value]


def decode(data, offset=DEFAULT_OFFSET):
    if len(data) > MAX_PACKET:
        raise ValueError('oversize packet')
    p = json.loads(data)
    if not isinstance(p, dict) or type(p.get('schema')) is not int or p.get('schema') != 1:
        raise ValueError('unsupported schema')
    if not isinstance(p.get('session'), str) or len(p['session']) != 32 or any(c not in '0123456789abcdef' for c in p['session']):
        raise ValueError('invalid session')
    for key in ('seq', 'stamp_ns', 'beams'):
        if type(p.get(key)) is not int or p[key] < 0:
            raise ValueError(f'invalid {key}')
    if p['stamp_ns'] >= 2**31 * 1_000_000_000 or p['beams'] > 3:
        raise ValueError('timestamp or beam count out of range')
    for key in ('has_dvl', 'valid', 'has_truth'):
        if type(p.get(key)) is not bool:
            raise ValueError(f'invalid {key}')
    for key in ('gyro', 'accel', 'velocity', 'dvl_offset'):
        p[key] = vector(p.get(key), 3, key)
    if any(abs(a-b) > 1e-5 for a, b in zip(p['dvl_offset'], offset)):
        raise ValueError('sensor offset differs from configured TF')
    for key in ('gyro_variance', 'accel_variance', 'depth_variance', 'velocity_variance'):
        p[key] = number(p.get(key), key)
        if p[key] <= 0:
            raise ValueError(f'{key} must be positive')
    for key in ('depth', 'altitude'):
        p[key] = number(p.get(key), key)
    if not isinstance(p.get('reason'), str) or len(p['reason']) > 64:
        raise ValueError('invalid status')
    if p['valid'] and (not p['has_dvl'] or p['beams'] != 3 or p['reason'] != 'bottom_lock' or p['altitude'] <= 0):
        raise ValueError('inconsistent bottom lock')
    if p['has_truth']:
        truth = p.get('truth')
        if not isinstance(truth, dict):
            raise ValueError('invalid truth')
        truth['position'] = vector(truth.get('position'), 3, 'truth position')
        truth['velocity'] = vector(truth.get('velocity'), 3, 'truth velocity')
        truth['angular_velocity'] = vector(truth.get('angular_velocity'), 3, 'truth angular velocity')
        truth['orientation'] = vector(truth.get('orientation'), 4, 'truth quaternion')
        if abs(sum(x*x for x in truth['orientation'])-1.0) > 1e-3:
            raise ValueError('truth quaternion is not normalized')
    else:
        # JsonUtility serializes null inline classes as objects containing empty arrays.
        p['truth'] = None
    return p


def compensate_lever_arm(velocity, gyro, offset):
    """v_origin = v_sensor - omega cross r, all expressed in body FLU."""
    x, y, z = gyro
    a, b, c = offset
    cross = (y*c-z*b, z*a-x*c, x*b-y*a)
    return [v-w for v, w in zip(velocity, cross)]


class SequenceGuard:
    """Reject replays, and never splice a second Unity run into an estimator run."""
    def __init__(self):
        self.session = None
        self.sequence = -1
        self.stamp = -1
        self.last_received = None

    def accept(self, packet):
        if self.session is not None and packet['session'] != self.session:
            raise ValueError('Unity session changed: restart the ROS launch to reset the estimator')
        if packet['seq'] <= self.sequence or packet['stamp_ns'] <= self.stamp:
            raise ValueError('duplicate or out-of-order packet')
        self.session = packet['session']
        self.sequence = packet['seq']
        self.stamp = packet['stamp_ns']
        self.last_received = time.monotonic()

    def fresh(self, timeout=0.5):
        return self.last_received is not None and time.monotonic()-self.last_received < timeout
