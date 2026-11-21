"""Pre-dive requirements for running the prequal mission on the physical vehicle (ArduSub 4.x)."""

# The mission streams MANUAL_CONTROL and GCS heartbeats from the topside laptop, so a crashed
# laptop or a cut tether must stop the thrusters, and a leak must bring the vehicle up.
FAILSAFES = {
    'FS_PILOT_INPUT': ({2}, 'pilot-input loss must disarm (2)'),
    'FS_GCS_ENABLE': ({2, 4}, 'ground-station loss must disarm (2) or surface (4)'),
    'FS_LEAK_ENABLE': ({2}, 'a leak must enter surface mode (2)'),
}
TIMEOUTS = {'FS_PILOT_TIMEOUT': 3.0, 'FS_GCS_TIMEOUT': 5.0}
REQUESTED = (*FAILSAFES, *TIMEOUTS, 'BATT_MONITOR')


def hardware_problems(params, firmware_major, voltage, min_voltage):
    """Every reason the vehicle is not fit for an autonomous dive; empty when it is."""
    problems = []
    if firmware_major != 4:
        problems.append('expected ArduSub 4.x firmware')
    for name, (allowed, why) in FAILSAFES.items():
        if name not in params:
            problems.append(f'{name} not read yet')
        elif int(params[name]) not in allowed:
            problems.append(f'{name}={int(params[name])}: {why}')
    for name, limit in TIMEOUTS.items():
        if name in params and params[name] > limit:
            problems.append(f'{name}={params[name]:g} s is longer than {limit:g} s')
    if not params.get('BATT_MONITOR'):
        problems.append('BATT_MONITOR is off, so battery voltage is unknown')
    elif voltage is None:
        problems.append('no battery voltage reported yet')
    elif voltage < min_voltage:
        problems.append(f'battery {voltage:.2f} V is below {min_voltage:.2f} V')
    return problems


def limit(value, magnitude):
    return max(-magnitude, min(magnitude, value))
