from club_rov_navigation.mission_safety import hardware_problems, limit

SAFE = {'FS_PILOT_INPUT': 2, 'FS_GCS_ENABLE': 4, 'FS_LEAK_ENABLE': 2,
        'FS_PILOT_TIMEOUT': 1, 'FS_GCS_TIMEOUT': 5, 'BATT_MONITOR': 4}


def test_safe_vehicle_passes():
    assert hardware_problems(SAFE, 4, 15.8, 14.0) == []


def test_default_warn_only_failsafes_are_refused():
    params = dict(SAFE, FS_PILOT_INPUT=1, FS_GCS_ENABLE=1, FS_LEAK_ENABLE=1)
    problems = hardware_problems(params, 4, 15.8, 14.0)
    assert len(problems) == 3 and all(p.split('=')[0] in params for p in problems)


def test_unread_params_battery_and_firmware_are_refused():
    assert any('not read' in p for p in hardware_problems({}, 4, 15.8, 14.0))
    assert any('below' in p for p in hardware_problems(SAFE, 4, 13.1, 14.0))
    assert any('no battery' in p for p in hardware_problems(SAFE, 4, None, 14.0))
    assert any('BATT_MONITOR' in p for p in hardware_problems(dict(SAFE, BATT_MONITOR=0), 4, 15.8, 14.0))
    assert any('firmware' in p for p in hardware_problems(SAFE, 3, 15.8, 14.0))
    assert any('longer' in p for p in hardware_problems(dict(SAFE, FS_GCS_TIMEOUT=30), 4, 15.8, 14.0))


def test_limit():
    assert limit(.9, .6) == .6 and limit(-.9, .6) == -.6 and limit(.2, .6) == .2
