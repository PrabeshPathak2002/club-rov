import pytest

from club_rov_navigation.dvl_delta import DeltaIntegrator


def test_first_sample_only_primes():
    d = DeltaIntegrator()
    assert d.add_velocity(0.0, (1, 0, 0), True) is None


def test_position_and_angle_deltas_are_frd():
    d = DeltaIntegrator()
    d.add_velocity(0.0, (0, 0, 0), True)
    for i in range(11):
        d.add_gyro(i*0.01, (0, 0, 0.5))  # yaw left in FLU
    dt, angle, position = d.add_velocity(0.1, (0.2, 0.1, -0.05), True)
    assert dt == pytest.approx(0.1)
    assert position == pytest.approx([0.02, -0.01, 0.005])
    assert angle == pytest.approx([0, 0, -0.05])


def test_lost_lock_and_long_gaps_send_nothing():
    d = DeltaIntegrator()
    d.add_velocity(0.0, (1, 0, 0), True)
    assert d.add_velocity(0.1, (0, 0, 0), False) is None
    assert d.add_velocity(0.2, (1, 0, 0), True) is None  # re-primes after lost lock
    assert d.add_velocity(0.3, (1, 0, 0), True) is not None
    assert d.add_velocity(1.0, (1, 0, 0), True) is None  # gap longer than 0.5 s
