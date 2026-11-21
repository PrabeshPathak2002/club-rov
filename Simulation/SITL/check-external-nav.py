"""Guarded SYSID 42 simulation test; never use against a physical vehicle.

Keep the DVL/ROS pipeline running. May be run with a modest Unity water current.
Always disarms; records evidence under the current (external, non-Git) run folder.
"""
import json
import math
import time
from pymavlink import mavutil


link = mavutil.mavlink_connection('udpin:127.0.0.1:14551', source_system=255)
heartbeat = link.wait_heartbeat(timeout=15)
if not heartbeat or heartbeat.get_srcSystem() != 42 or heartbeat.type != 12:
    raise RuntimeError('Expected isolated SYSID 42 submarine')
link.target_system, link.target_component = 42, 1
latest, samples, texts = {}, [], []


def pump(seconds, x=0, y=0):
    end = time.monotonic()+seconds
    while time.monotonic() < end:
        link.mav.heartbeat_send(6, 8, 0, 0, 4)
        link.mav.manual_control_send(42, x, y, 500, 0, 0)
        until = time.monotonic()+.05
        while time.monotonic() < until:
            m = link.recv_match(blocking=True, timeout=.01)
            if not m or m.get_srcSystem() != 42 or m.get_srcComponent() != 1:
                continue
            latest[m.get_type()] = m.to_dict()
            if m.get_type() == 'LOCAL_POSITION_NED':
                samples.append(m.to_dict())
            if m.get_type() == 'STATUSTEXT':
                texts.append(m.text)


def phase(sim_seconds, **inputs):
    first = latest['LOCAL_POSITION_NED']['time_boot_ms']
    deadline = time.monotonic()+sim_seconds*12+15
    start = len(samples)
    while latest['LOCAL_POSITION_NED']['time_boot_ms']-first < sim_seconds*1000:
        if time.monotonic() > deadline:
            raise RuntimeError('Simulation clock stopped')
        pump(.1, **inputs)
    return samples[start:]


report = {}
try:
    link.mav.request_data_stream_send(42, 1, 0, 10, 1)
    link.mav.command_long_send(42, 1, 512, 0, 148, 0, 0, 0, 0, 0, 0)
    for name in ('SIM_GPS1_ENABLE', 'VISO_TYPE', 'EK3_SRC1_POSXY'):
        link.mav.param_request_read_send(42, 1, name.encode(), -1)
        pump(.5)
        m = latest.get('PARAM_VALUE', {})
        expected = {'SIM_GPS1_ENABLE': 0, 'VISO_TYPE': 1, 'EK3_SRC1_POSXY': 6}[name]
        if m.get('param_id') != name or m.get('param_value') != expected:
            raise RuntimeError('External-nav profile verification failed: '+name)
    pump(2)
    if latest.get('AUTOPILOT_VERSION', {}).get('flight_sw_version', 0) >> 8 != 0x040702:
        raise RuntimeError('Expected pinned Sub-4.7.2')
    deadline = time.monotonic()+120
    while not (latest.get('EKF_STATUS_REPORT', {}).get('flags', 0) & 16 and
               latest.get('GLOBAL_POSITION_INT', {}).get('lat', 0) != 0 and
               'LOCAL_POSITION_NED' in latest):
        if time.monotonic() > deadline:
            raise RuntimeError('EKF absolute position did not become valid: '+str(latest.get('EKF_STATUS_REPORT')))
        pump(.2)
    print('EKF absolute position valid; QGC global/local position messages present', flush=True)
    report['global_position'] = latest['GLOBAL_POSITION_INT']
    report['ekf_before'] = latest['EKF_STATUS_REPORT']
    link.set_mode('POSHOLD')
    pump(2)
    if latest['HEARTBEAT']['custom_mode'] != 16:
        raise RuntimeError('POSHOLD rejected: '+str(texts[-8:]))
    link.arducopter_arm()
    pump(3)
    if not latest['HEARTBEAT']['base_mode'] & 128:
        raise RuntimeError('Arming rejected: '+str(texts[-8:]))
    held = phase(8)
    first = held[0]
    drift = max(math.hypot(m['x']-first['x'], m['y']-first['y']) for m in held)
    print(f'8 simulation seconds position hold: max horizontal excursion {drift:.3f} m', flush=True)
    report['hold_excursion_m'] = drift
    report['hold'] = held
    if drift > .75:
        raise RuntimeError('Excessive position-hold excursion')
    moved = phase(3, x=350)
    settled = phase(8)
    travel = math.hypot(moved[-1]['x']-moved[0]['x'], moved[-1]['y']-moved[0]['y'])
    speed = math.hypot(settled[-1]['vx'], settled[-1]['vy'])
    print(f'Pilot reposition: {travel:.3f} m; final horizontal speed {speed:.3f} m/s', flush=True)
    report.update(pilot_travel_m=travel, final_speed_mps=speed, moved=moved, settled=settled)
    if travel < .15 or speed > .15:
        raise RuntimeError('Pilot reposition or stopping check failed')
    report['passed'] = True
finally:
    link.arducopter_disarm()
    pump(2)
    report['disarmed'] = not bool(latest.get('HEARTBEAT', {}).get('base_mode', 128) & 128)
    report['servo_outputs'] = latest.get('SERVO_OUTPUT_RAW')
    report['texts'] = texts
    with open('external-nav-check.json', 'w') as output:
        json.dump(report, output, indent=2)
    link.close()
    print('Finished; simulator disarmed='+str(report['disarmed']), flush=True)
