import math
import pytest
from club_rov_navigation.external_nav_frames import convert, pose_covariance


def test_heading_and_body_signs():
    p, q, v, a = convert((1,2,3), (0,0,0,1), (1,2,3), (4,5,6), math.pi/2)
    assert p == pytest.approx((2,1,-3))
    assert q == pytest.approx((math.sqrt(.5),0,0,math.sqrt(.5)))
    assert v == (1,-2,-3)
    assert a == (4,-5,-6)


def test_positive_ros_yaw_is_negative_ned_yaw():
    _, q, _, _ = convert((0,0,0), (0,0,math.sqrt(.5),math.sqrt(.5)), (0,0,0), (0,0,0), 0)
    assert q == pytest.approx((math.sqrt(.5),0,0,-math.sqrt(.5)))


def test_reject_corrupt_or_uncertain_pose():
    with pytest.raises(ValueError):
        convert((float('nan'),0,0), (0,0,0,1), (0,0,0), (0,0,0), 0)
    with pytest.raises(ValueError):
        convert((0,0,0), (0,0,0,2), (0,0,0), (0,0,0), 0)
    cov = [0.0]*36
    for i in range(6): cov[i*7] = .02
    result = pose_covariance(cov)
    assert [result[i] for i in (0,6,11,15,18,20)] == [.02]*6
    cov[0] = 5
    with pytest.raises(ValueError): pose_covariance(cov)
