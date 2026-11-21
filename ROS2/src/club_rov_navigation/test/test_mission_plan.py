import math
import pytest
from club_rov_navigation.mission_plan import command, goals, reached, wrap


def test_launch_heading_and_return_geometry():
    p = goals(2,3,math.pi/2,4,1.8)
    assert [name for name,_ in p] == ['DIVE','OUTBOUND','TURN','RETURN','SURFACE']
    assert p[1][1][:2] == pytest.approx((2,7))
    assert p[3][1][:2] == (2,3)
    assert abs(wrap(p[2][1][3]-math.pi/2)) == pytest.approx(math.pi)
    assert p[4][1][2] == .1


def test_bounded_body_commands_and_vertical_sign():
    # Launch-frame +X lies on starboard when the vehicle faces +Y.
    f,r,d,w = command(0,0,math.pi/2,1,(10,0,2,math.pi/2))
    assert f == pytest.approx(0,abs=1e-8)
    assert r == pytest.approx(.15)
    assert d == .18
    assert w == 0
    assert command(0,0,0,2,(0,0,.1,0))[2] == -.18
    assert command(0,0,0,1,(0,0,1,math.pi/2))[3] == -.30


def test_arrival_requires_heading_depth_and_low_speed():
    target=(0,0,1.8,0)
    assert reached(.1,.1,.1,1.81,.05,target)
    assert not reached(.1,.1,.1,1.81,.3,target)
    assert not reached(.1,.1,1,1.81,.05,target)
    assert not reached(.1,.1,.1,1.2,.05,target)


@pytest.mark.parametrize('distance,depth', [(0,1.8),(7,1.8),(4,3),(4,float('nan'))])
def test_reject_unsafe_or_nonfinite_goals(distance,depth):
    with pytest.raises(ValueError): goals(0,0,0,distance,depth)
