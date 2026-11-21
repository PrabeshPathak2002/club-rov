"""RoboSub prequalification behaviour without DVL position: depth, compass heading and a camera marker.

Pure logic, independent of ROS and MAVLink. Headings are radians, clockwise positive (ArduSub
ATTITUDE yaw). Commands are normalised: surge forward, sway starboard, heave up, yaw clockwise.
"""
import math
from dataclasses import dataclass


def wrap(angle):
    return (angle+math.pi) % (2*math.pi)-math.pi


def clamp(value, limit):
    return max(-limit, min(limit, value))


@dataclass(frozen=True)
class Config:
    depth: float = 1.6
    cruise: float = 0.5
    marker_diameter: float = 0.16
    gate_width: float = 2.0
    approach_distance: float = 2.6
    orbit_radius: float = 1.2
    orbit_sway: float = 0.45
    outbound_limit: float = 90.0
    return_scale: float = 1.2
    max_depth: float = 3.0

    def __post_init__(self):
        values = (self.depth, self.cruise, self.marker_diameter, self.gate_width, self.approach_distance, self.orbit_radius,
                  self.orbit_sway, self.outbound_limit, self.return_scale, self.max_depth)
        if not all(math.isfinite(v) and v > 0 for v in values):
            raise ValueError('Prequal settings must be finite and positive')
        if not .6 <= self.depth < self.max_depth or self.cruise > 1 or self.orbit_sway > 1:
            raise ValueError('Prequal depth or command limits out of range')
        if self.orbit_radius >= self.approach_distance:
            raise ValueError('Orbit radius must be smaller than the approach distance')


@dataclass(frozen=True)
class Marker:
    bearing: float
    angular_width: float

    def distance(self, diameter):
        return diameter/max(self.angular_width, 1e-6)


@dataclass(frozen=True)
class Command:
    surge: float = 0.0
    sway: float = 0.0
    heave: float = 0.0
    yaw: float = 0.0


class Prequal:
    """Dive, cruise toward the marker, circle it once, turn back and return through the gate.

    While both gate posts are in view the course is re-aimed at the gate centre; otherwise the
    vehicle holds the last course heading.
    """

    def __init__(self, config=Config()):
        self.config = config
        self.phase, self.reason = 'DIVE', 'Dive to mission depth'
        self.phase_at = self.stable_at = None
        self.course = self.entry_heading = None
        self.gate_close = False
        self.gate_seen_at = None
        self.cruise_started = self.cruise_seconds = None
        self.circled, self.sight = 0.0, None
        self.sightings = []
        self.marker_seen_at = None
        self.last_bearing = 0.0
        self.outcome = None

    def transition(self, phase, reason, now):
        self.phase, self.reason, self.phase_at, self.stable_at = phase, reason, now, None

    def abort(self, reason, now):
        if self.phase not in ('SURFACE', 'DONE'):
            self.outcome = 'aborted'
            self.transition('SURFACE', reason, now)

    def heave(self, depth, target):
        # ArduSub depth hold ignores stick within its throttle deadzone (about +/-0.25), so step
        # past it when correcting and leave it neutral to hold.
        error = depth-target
        return 0.0 if abs(error) < .1 else math.copysign(min(.8, .3+.8*abs(error)), error)

    def hold(self, heading, target):
        return clamp(1.4*wrap(target-heading), .6)

    def steady(self, condition, now, seconds):
        if not condition:
            self.stable_at = None
            return False
        if self.stable_at is None:
            self.stable_at = now
        return now-self.stable_at >= seconds

    def steer_by_gate(self, now, heading, gate):
        if gate is None or gate.distance(self.config.gate_width) > 12:
            return
        self.course, self.gate_seen_at = wrap(heading+gate.bearing), now
        self.gate_close = self.gate_close or gate.distance(self.config.gate_width) < 3

    def step(self, now, depth, heading, marker=None, gate=None):
        """Advance one control tick. `now` is monotonic seconds; `marker`/`gate` are None when not detected."""
        c = self.config
        if not all(math.isfinite(v) for v in (now, depth, heading)):
            raise ValueError('Invalid vehicle state')
        if self.phase_at is None:
            self.phase_at, self.course = now, heading
        if marker is not None:
            self.marker_seen_at, self.last_bearing = now, marker.bearing
        if depth > c.max_depth:
            self.abort('Depth limit exceeded', now)
        elapsed = now-self.phase_at
        target = c.depth

        if self.phase == 'DIVE':
            if self.steady(abs(depth-target) < .15, now, 2):
                self.cruise_started = now
                self.transition('OUTBOUND', 'Cruise on launch heading through the gate', now)
            elif elapsed > 30:
                self.abort('Could not reach mission depth', now)
            return Command(heave=self.heave(depth, target), yaw=self.hold(heading, self.course))

        if self.phase == 'OUTBOUND':
            if marker is not None:
                self.sightings = [(t, s) for t, s in self.sightings if now-t <= 3]+[(now, wrap(heading+marker.bearing))]
            if marker is not None and marker.distance(c.marker_diameter) <= c.approach_distance:
                self.cruise_seconds = now-self.cruise_started
                self.sight = wrap(heading+marker.bearing)
                # The return leg reverses this line; one sample can be several degrees off, which is
                # over a metre of miss at the gate.
                self.entry_heading = math.atan2(sum(math.sin(s) for _, s in self.sightings),
                                                sum(math.cos(s) for _, s in self.sightings))
                self.transition('ORBIT', 'Marker reached; circle it once', now)
            elif elapsed > c.outbound_limit:
                self.abort('Marker not found before outbound limit', now)
            if marker is not None:
                yaw = clamp(1.6*marker.bearing, .5)
            else:
                self.steer_by_gate(now, heading, gate)
                yaw = self.hold(heading, self.course)
            return Command(surge=c.cruise, heave=self.heave(depth, target), yaw=yaw)

        if self.phase == 'ORBIT':
            lost = now-self.marker_seen_at
            if lost > 8:
                self.abort('Marker lost while circling', now)
                return Command(heave=self.heave(depth, target))
            if marker is None:
                # Rotate toward the side where the marker was last seen.
                return Command(heave=self.heave(depth, target), yaw=math.copysign(.35, self.last_bearing))
            # Count the circle by line of sight and finish back on the entry line, so the return
            # leg retraces the outbound line.
            sight = wrap(heading+marker.bearing)
            self.circled += wrap(sight-self.sight)
            self.sight = sight
            if abs(self.circled) >= 2*math.pi:
                self.transition('ALIGN', 'Circle complete; settle back onto the entry line', now)
            elif elapsed > 120:
                self.abort('Circle took too long', now)
            distance = marker.distance(c.marker_diameter)
            return Command(surge=clamp(.6*(distance-c.orbit_radius), .3), sway=c.orbit_sway,
                           heave=self.heave(depth, target), yaw=clamp(1.8*marker.bearing, .6))

        if self.phase == 'ALIGN':
            # Sway momentum carries the vehicle past the entry line; any sideways offset left here
            # is carried all the way back to the gate.
            error = None
            if marker is not None:
                self.sight = wrap(heading+marker.bearing)
                error = wrap(self.sight-self.entry_heading)
            # Close to the marker the line of sight jitters a few degrees; 0.1 rad is ~0.12 m off the line.
            # Hold long enough that a vehicle still drifting through the line can't qualify.
            if self.steady(error is not None and abs(error) < .1, now, 2) or elapsed > 25 \
                    or now-self.marker_seen_at > 8:
                self.course, self.gate_close, self.gate_seen_at = wrap(self.entry_heading+math.pi), False, None
                self.transition('TURN', 'On the entry line; turn back toward the gate', now)
            if marker is None:
                return Command(heave=self.heave(depth, target), yaw=math.copysign(.35, self.last_bearing))
            distance = marker.distance(c.marker_diameter)
            return Command(surge=clamp(.6*(distance-c.orbit_radius), .3),
                           sway=clamp(-math.copysign(1.5, self.circled)*error, .3),
                           heave=self.heave(depth, target), yaw=clamp(1.0*marker.bearing, .4))

        if self.phase == 'TURN':
            if self.steady(abs(wrap(heading-self.course)) < .08, now, 1):
                self.transition('RETURN', 'Cruise back through the gate', now)
            elif elapsed > 30:
                self.abort('Turn did not settle', now)
            return Command(heave=self.heave(depth, target), yaw=self.hold(heading, self.course))

        if self.phase == 'RETURN':
            self.steer_by_gate(now, heading, gate)
            expected = self.cruise_seconds*c.return_scale
            # Both posts leave the camera's view about 1.2 m before the gate; keep going to clear it.
            if self.gate_close and now-self.gate_seen_at > 6:
                self.outcome = 'completed'
                self.transition('SURFACE', 'Passed back through the gate', now)
            elif elapsed >= (1.5*expected if self.gate_seen_at is not None else expected):
                self.outcome = 'completed'
                self.transition('SURFACE', 'Return leg complete (timed; gate not confirmed)', now)
            return Command(surge=c.cruise, heave=self.heave(depth, target), yaw=self.hold(heading, self.course))

        if self.phase == 'SURFACE':
            if self.steady(depth < .2, now, 1) or elapsed > 40:
                self.outcome = self.outcome or 'aborted'
                self.transition('DONE', self.reason, now)
            return Command(heave=.8)

        return Command()
