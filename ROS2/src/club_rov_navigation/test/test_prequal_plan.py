import math

import numpy as np
import pytest

from club_rov_navigation.marker_detector import detect_gate, detect_marker
from club_rov_navigation.prequal_plan import Config, Marker, Prequal, wrap


def picture(left, width, colour=(255, 110, 20), height=120):
    image = np.full((180, 320, 3), (20, 90, 110), np.uint8)
    image[30:30+height, left:left+width] = colour
    return image


def test_gate_detector_needs_both_posts_and_reports_centre():
    image = np.full((180, 320, 3), (20, 90, 110), np.uint8)
    image[0:130, 100:104] = image[0:130, 240:244] = image[126:130, 100:244] = (200, 150, 60)
    gate = detect_gate(image, fx=198.0)
    assert gate.bearing == pytest.approx(math.atan2(12, 198))
    image[:, 236:] = (20, 90, 110)  # right post out of view: gate span touches nothing reliable
    image[126:130, 100:320] = (200, 150, 60)
    assert detect_gate(image, fx=198.0) is None


def test_gate_detector_accepts_haze_tinted_post():
    # Pixel values from a SITL return-leg frame: the far post reads hue ~46, saturation ~56.
    image = np.full((180, 320, 3), (20, 90, 110), np.uint8)
    image[0:130, 240:244] = image[126:130, 170:244] = (200, 150, 60)
    image[0:130, 100:104] = image[126:130, 100:170] = (68, 77, 60)
    assert detect_gate(image, fx=198.0).bearing == pytest.approx(math.atan2(12, 198))
    image[110:130, 100:104] = image[126:130, 104:200] = (20, 90, 110)  # faint corner lost entirely
    assert detect_gate(image, fx=198.0).bearing == pytest.approx(math.atan2(12, 198))


@pytest.mark.parametrize('box', [
    (120, 124, 60, 200),  # bar fragment
    (60, 90, 150, 180),  # solid blob
    (40, 140, 200, 204),  # single post
])
def test_gate_detector_rejects_fragments(box):
    image = np.full((180, 320, 3), (20, 90, 110), np.uint8)
    top, bottom, left, right = box
    image[top:bottom, left:right] = (200, 150, 60)
    assert detect_gate(image, fx=198.0) is None


def test_detector_reports_bearing_and_width_of_tall_orange_blob():
    marker = detect_marker(picture(200, 8), fx=198.0)
    assert marker.bearing == pytest.approx(math.atan2(44, 198))
    assert marker.angular_width == pytest.approx(2*math.atan2(4, 198))
    assert marker.distance(.16) == pytest.approx(.16/marker.angular_width)


@pytest.mark.parametrize('image', [
    picture(200, 8, colour=(30, 200, 220)),  # cyan pool tile
    picture(200, 8, colour=(200, 150, 60)),  # gold gate leg
    picture(100, 60, height=40),  # wide, not a pole
    picture(100, 2, height=4),  # speck
])
def test_detector_ignores_non_marker_blobs(image):
    assert detect_marker(image, fx=198.0) is None


class Pool:
    """Planar kinematic pool: gate bar at y=0 (|x| < 1, 1 m deep), 0.16 m marker 10 m beyond it."""

    def __init__(self, visibility=8.0, heading_noise=0.0, seed=1, x=0.0, heading=0.0, lag=0.0):
        self.x, self.y, self.depth, self.heading = x, -3.0, .1, heading
        self.lag, self.vx, self.vy = lag, 0.0, 0.0
        self.visibility, self.heading_noise = visibility, heading_noise
        self.random = np.random.default_rng(seed)
        self.crossings, self.circled = [], 0.0
        self.first_angle = self.last_angle = math.atan2(self.x, self.y-10)

    def windings(self):
        """Winding number of the path around the marker, closed back to the start point."""
        return round((self.circled + wrap(self.first_angle-self.last_angle))/(2*math.pi))

    def marker(self):
        dx, dy = -self.x, 10-self.y
        distance = math.hypot(dx, dy)
        bearing = wrap(math.atan2(dx, dy)-self.heading)
        if distance > self.visibility or abs(bearing) > math.radians(38):
            return None
        return Marker(bearing + self.random.normal(0, .01), 2*math.atan2(.08, distance))

    def gate(self):
        bearings = []
        for post in (-1, 1):
            dx, dy = post-self.x, -self.y
            bearing = wrap(math.atan2(dx, dy)-self.heading)
            if math.hypot(dx, dy) > self.visibility or abs(bearing) > math.radians(38):
                return None
            bearings.append(bearing)
        return Marker(sum(bearings)/2, abs(bearings[1]-bearings[0]))

    def move(self, command, dt):
        heading = self.heading
        forward, right = .6*command.surge, .5*command.sway
        # World-frame velocity relaxes toward the command, so momentum survives a turn in place.
        blend = 1.0 if self.lag <= 0 else min(1.0, dt/self.lag)
        self.vx += blend*(forward*math.sin(heading) + right*math.cos(heading) - self.vx)
        self.vy += blend*(forward*math.cos(heading) - right*math.sin(heading) - self.vy)
        y = self.y + dt*self.vy
        self.x += dt*self.vx
        if (self.y < 0) != (y < 0):
            self.crossings.append(('out' if y > self.y else 'in', self.x, self.depth))
        self.y = y
        self.heading = wrap(heading + dt*1.05*command.yaw)
        heave = 0 if abs(command.heave) < .25 else command.heave  # depth-hold stick deadzone
        self.depth = max(0.0, self.depth - dt*.5*heave)
        angle = math.atan2(self.x, self.y-10)
        self.circled += wrap(angle-self.last_angle)
        self.last_angle = angle


@pytest.mark.parametrize('visibility,noise,x,heading,lag', [
    (8, 0, 0, 0, 0), (5, 0, 0, 0, 0), (8, .02, 0, 0, 0),
    (8, 0, .7, math.radians(-8), 0),  # placed off-centre and mis-aimed
    (5, .02, -.5, math.radians(10), 0),
    (8, .02, 0, 0, 2.0), (5, .02, .5, math.radians(-6), 3.0),  # vehicle keeps drifting after a command
])
def test_closed_loop_prequal_passes_gate_circles_marker_and_returns(visibility, noise, x, heading, lag):
    pool, mission, now = Pool(visibility, noise, x=x, heading=heading, lag=lag), Prequal(), 0.0
    while mission.phase != 'DONE' and now < 600:
        heading = pool.heading + pool.random.normal(0, noise)
        command = mission.step(now, pool.depth, heading, pool.marker(), pool.gate())
        pool.move(command, .1)
        now += .1
    assert mission.outcome == 'completed', mission.reason
    assert [c[0] for c in pool.crossings] == ['out', 'in']
    for _, x, depth in pool.crossings:
        assert abs(x) < .8 and depth > 1.2
    assert abs(pool.windings()) == 1
    assert pool.depth < .2


def test_missing_marker_aborts_to_surface():
    pool, mission, now = Pool(visibility=0), Prequal(Config(outbound_limit=20)), 0.0
    while mission.phase != 'DONE' and now < 200:
        pool.move(mission.step(now, pool.depth, pool.heading, None), .1)
        now += .1
    assert mission.outcome == 'aborted'
    assert 'Marker not found' in mission.reason
    assert pool.depth < .2


def test_depth_limit_aborts_immediately():
    mission = Prequal()
    mission.step(0, .5, 0)
    mission.step(.1, 3.2, 0)
    assert mission.phase == 'SURFACE' and mission.reason == 'Depth limit exceeded'


@pytest.mark.parametrize('settings', [dict(depth=.3), dict(cruise=1.5), dict(orbit_radius=3), dict(return_scale=0)])
def test_rejects_unsafe_settings(settings):
    with pytest.raises(ValueError):
        Config(**settings)
