"""Learned visual geometry with an internal sensor-derived course tracker.

Only camera, IMU/pressure and DVL enter predict(). Scoring statistics never do.
"""
import math
import hashlib
from pathlib import Path
import numpy as np
import onnxruntime as ort
from marker_measurements import gate_measurement, hover_measurements, dock_bearing


def runtime_fingerprint(policy):
    paths = [Path(policy), Path(policy).with_name('CourseMap.npz'),
             Path(__file__), Path(__file__).with_name('marker_measurements.py')]
    return {path.name: hashlib.sha256(path.read_bytes()).hexdigest() for path in paths if path.exists()}


def rotation(yaw):
    c, s = math.cos(yaw), math.sin(yaw)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])


def wrap(angle):
    return (angle + math.pi) % (2 * math.pi) - math.pi


class TrackedGeometry:
    def __init__(self, path):
        options = ort.SessionOptions()
        options.intra_op_num_threads = options.inter_op_num_threads = 1
        self.model = ort.InferenceSession(str(path), options, providers=['CPUExecutionProvider'])
        if 'goal_phase' not in [item.name for item in self.model.get_inputs()]:
            raise ValueError('Tracked geometry requires a goal-conditioned estimator')
        map_path = Path(path).with_name('CourseMap.npz')
        self.map_offsets = np.load(map_path)['offsets'] if map_path.exists() else None
        self.reset()

    def reset(self):
        self.memory = np.zeros((1, 1, 136), np.float32)
        self.phase, self.hold, self.steps = 0, 0., 0
        self.goal, self.goal_yaw = None, 0.
        self.locked, self.landing = False, False
        self.gate_aligned = False
        self.standoff, self.approach_side = None, 0
        self.position = np.zeros(2)
        self.velocity_world = np.zeros(3)
        self.gravity_body = np.array([0., 9.81, 0.])
        self.previous_dvl = None
        self.previous_depth, self.pressure_velocity = None, 0.
        self.hover_observations = []
        self.map_anchor, self.map_yaw, self.map_observations = None, 0., {}
        self.floor_samples, self.platform_depth = [], None
        self.dock_stable, self.dock_velocity = 0., np.zeros(2)
        self.debug = {}

    def predict(self, image, sensors):
        if sensors[15] > .5:
            depth = float(sensors[6]*4)
            if self.previous_depth is not None:
                self.pressure_velocity = .7*self.pressure_velocity - .3*(depth-self.previous_depth)/.1
            self.previous_depth = depth
        yaw = float(self.memory[0, 0, 130] - sensors[5] * .2 * sensors[15])
        basis = rotation(yaw)
        omega = np.array([sensors[4]*2, -sensors[5]*2, -sensors[3]*2])
        self.gravity_body -= np.cross(omega, self.gravity_body) * .1
        norm = np.linalg.norm(self.gravity_body)
        if norm > 1: self.gravity_body *= 9.81/norm
        measured_accel = np.array([-sensors[1]*20, sensors[2]*20, sensors[0]*20])
        if sensors[11] > .5:
            body_velocity = np.array([-sensors[8]*2, sensors[9]*2, sensors[7]*2])
            if self.previous_dvl is not None:
                acceleration = (body_velocity-self.previous_dvl)/.1 + np.cross(omega, body_velocity)
                self.gravity_body = .99*self.gravity_body + .01*(measured_accel-acceleration)
            self.previous_dvl = body_velocity.copy()
            self.velocity_world = basis @ body_velocity
        elif sensors[15] > .5:
            self.previous_dvl = None
            if self.landing:
                self.velocity_world[[0, 2]] *= .5
            else:
                self.velocity_world += basis @ (measured_accel-self.gravity_body) * .1
            self.velocity_world = np.clip(self.velocity_world, -1.5, 1.5)
        self.position += self.velocity_world[[0, 2]]*.1
        if sensors[11] < .5:
            # Bridge bottom-lock gaps with measured inertial acceleration. Do
            # not change the DVL validity bit presented to the neural estimator.
            self.memory[0, 0, 128] = self.position[0]
            self.memory[0, 0, 129] = self.position[1]
        query = np.eye(5, dtype=np.float32)[self.phase][None]
        _, memory, estimate, phase_logits = self.model.run(None, {
            'obs_0': image[None], 'obs_1': sensors[None], 'recurrent_in': self.memory, 'goal_phase': query})
        self.memory = memory
        self.memory[0, 0, 128] = self.position[0]
        self.memory[0, 0, 129] = self.position[1]
        # The first observation has no fresh pressure; wait one sample before
        # initializing a target in the measured odometry frame.
        if sensors[15] < .5:
            return np.zeros(4, np.float32)
        pose = np.array([memory[0, 0, 128], -sensors[6]*4, memory[0, 0, 129]])
        yaw = float(memory[0, 0, 130])
        basis = rotation(yaw)
        state = estimate[0]
        if self.phase < 3 and sensors[11] > .5:
            self.floor_samples.append(float(sensors[6]*4 + sensors[10]*4 + .18))
            self.floor_samples = self.floor_samples[-200:]
        proposed = pose + basis @ state[:3]
        if getattr(self, 'map_offsets', None) is not None and self.map_anchor is not None:
            proposed = self.map_anchor + rotation(self.map_yaw) @ self.map_offsets[self.phase]
        proposed_yaw = yaw + math.atan2(float(state[3]), float(state[4]))
        for observation in hover_measurements(image):
            point = pose + basis @ observation
            match = next((row for row in self.hover_observations if np.linalg.norm(row[0]-point) < .5), None)
            if match is None:
                self.hover_observations.append([point, 1])
            else:
                match[0] = .8*match[0]+.2*point
                match[1] += 1
        if self.goal is None:
            self.goal = proposed
            # Published mission headings with launch-yaw zero calibration;
            # never read the simulator's target rotation or start transform.
            self.goal_yaw = self.map_yaw + (0. if self.phase < 4 else math.pi)
            self.approach_side = 1 if state[0] >= 0 else -1
            self.standoff = min(3.0, max(.8, float(state[2])))
        offset = basis.T @ (self.goal - pose)
        distance = np.linalg.norm(offset)
        horizontal = np.linalg.norm(offset[[0, 2]])
        if self.phase == 3:
            candidates = [row for row in self.hover_observations if row[1] >= 3 and np.linalg.norm(row[0]-self.goal) < 1.2]
            if candidates:
                proposed = min(candidates, key=lambda row: np.linalg.norm(row[0]-self.goal))[0]
        if self.phase == 4 and not self.locked:
            bearing = dock_bearing(image)
            if bearing is not None:
                corrected = np.array([math.sin(bearing)*horizontal, offset[1], math.cos(bearing)*horizontal])
                proposed = pose+basis@corrected
        measurement = gate_measurement(image, float(offset[2]), self.approach_side, float(offset[0])) \
            if self.phase < 3 and not self.gate_aligned and offset[2] > 1.8 else None
        if measurement is not None:
            measured = np.array([measurement[0], offset[1] if measurement[1] is None else measurement[1], measurement[2]])
            proposed = pose + basis @ measured
            self.goal = .5*self.goal + .5*proposed
        elif not self.locked:
            self.goal = .85*self.goal + .15*proposed
        if not self.locked:
            if (self.phase < 3 and distance < 1.2) or (self.phase == 3 and distance < 1) or (self.phase == 4 and horizontal < .9):
                self.locked = True
        offset = basis.T @ (self.goal-pose)
        angle = wrap(self.goal_yaw-yaw)
        velocity = basis.T @ self.velocity_world
        velocity[1] = sensors[9]*2 if sensors[11] > .5 else self.pressure_velocity
        target = offset.copy()
        if self.phase < 3:
            normal = np.array([math.sin(angle), 0, math.cos(angle)])
            longitudinal = math.sin(angle)*offset[0] + math.cos(angle)*offset[2]
            lateral = math.cos(angle)*offset[0] - math.sin(angle)*offset[2]
            lateral_speed = math.cos(angle)*velocity[0] - math.sin(angle)*velocity[2]
            if abs(lateral) < .18 and abs(offset[1]) < .2 and abs(lateral_speed) < .15:
                self.gate_aligned = True
                self.locked = True
            # Align at a stand-off point before committing through an opening.
            # Direct saturated diagonals can cross the plane beside a gate.
            target += normal * (.8 if self.gate_aligned else -self.standoff)
            if longitudinal < -.35 and abs(lateral) < .4 and abs(offset[1]) < .4:
                self.advance()
        elif self.phase == 3:
            ready = np.linalg.norm(offset) < .2 and abs(angle) < math.radians(7) and np.linalg.norm(velocity) < .08 and sensors[11] > .5
            self.hold = self.hold + .1 if ready else 0
            if self.hold >= 3.8:
                self.advance()
        else:
            floor = float(np.median(self.floor_samples)) if self.floor_samples else None
            self.dock_velocity = .7*self.dock_velocity + .3*velocity[[0, 2]]
            if floor is not None and sensors[11] > .5 and horizontal < .6:
                surface = float(sensors[6]*4 + sensors[10]*4 + .18)
                if surface < floor-.15:
                    self.platform_depth = surface if self.platform_depth is None else .8*self.platform_depth+.2*surface
            ready = horizontal < .1 and np.linalg.norm(self.dock_velocity) < .02 and abs(angle) < math.radians(7)
            self.dock_stable = self.dock_stable+.1 if ready else 0
            if not self.landing and self.dock_stable >= .6 and self.platform_depth is not None:
                self.landing = True
                # Vehicle half-height .15 m plus .02 m bottom clearance.
                self.goal[1] = -(self.platform_depth-.17)
            if self.landing:
                target[1] = self.goal[1]-pose[1]
            elif floor is not None:
                # Conservative clearance retains DVL lock above the raised dock.
                target[1] = -(floor-1.38)-pose[1]
            else:
                target[1] += .8
        command = np.clip(.55*target-velocity, -.6, .6)
        if self.phase == 4 and self.landing:
            command[[0, 2]] = np.clip(-velocity[[0, 2]], -.15, .15) if sensors[11] > .5 else 0
        steering_angle = math.atan2(offset[0], offset[2]) if self.phase == 4 and horizontal > .9 and not self.landing else angle
        turn = np.clip(.8*steering_angle + .8*sensors[5]*2, -.5, .5)
        self.steps += 1
        self.debug = dict(estimated_phase=self.phase, estimated_offset_m=offset.tolist(),
                          estimated_heading_error_rad=angle, internal_hold_seconds=self.hold,
                          target_locked=self.locked, landing=self.landing, proposed_state=state.tolist(),
                          measured_yaw_rad=yaw, heading_setpoint_rad=self.goal_yaw,
                          ring_clusters=len(self.hover_observations), platform_depth_m=self.platform_depth,
                          dock_stable_seconds=self.dock_stable)
        return np.r_[command, turn].astype(np.float32)

    def advance(self):
        if self.phase < 3 and getattr(self, 'map_offsets', None) is not None:
            self.map_observations[self.phase] = self.goal.copy()
            if len(self.map_observations) > 1:
                angles, weights = [], []
                keys = sorted(self.map_observations)
                for i, key in enumerate(keys):
                    for other in keys[i+1:]:
                        measured = self.map_observations[other]-self.map_observations[key]
                        nominal = self.map_offsets[other]-self.map_offsets[key]
                        angles.append(wrap(math.atan2(measured[0], measured[2])-math.atan2(nominal[0], nominal[2])))
                        weights.append(np.linalg.norm(nominal[[0,2]]))
                self.map_yaw = math.atan2(np.dot(weights, np.sin(angles)), np.dot(weights, np.cos(angles)))
            self.map_anchor = np.median([point-rotation(self.map_yaw)@self.map_offsets[key]
                                        for key, point in self.map_observations.items()], axis=0)
        self.phase = min(4, self.phase+1)
        self.goal, self.hold, self.locked, self.landing = None, 0., False, False
        self.gate_aligned = False
        self.standoff = None
