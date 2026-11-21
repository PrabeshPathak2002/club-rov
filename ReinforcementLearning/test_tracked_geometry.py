import unittest
import numpy as np
from tracked_geometry import TrackedGeometry, rotation


class FakeEstimator:
    def run(self, names, feed):
        memory = feed['recurrent_in'].copy()
        memory[0, 0, 129] += feed['obs_1'][0, 7] * .2
        mode = int(feed['goal_phase'].argmax())
        state = np.array([[0., 0., 1. if mode < 3 else 0., 0., 1.]], np.float32)
        # Deliberately claims dock phase immediately: tracker must ignore this.
        logits = np.array([[0., 0., 0., 0., 100.]], np.float32)
        return np.zeros((1, 4)), memory, state, logits


class TrackedGeometryTests(unittest.TestCase):
    def test_learned_map_registration_uses_observed_gate_positions(self):
        policy = self.make_policy()
        policy.map_offsets = np.array([[0,0,0],[3,0,4],[-2,0,6],[0,0,8],[3,-1,7]], float)
        anchor = np.array([1., -1.6, 2.])
        policy.goal = anchor.copy()
        policy.advance()
        policy.goal = anchor + rotation(.1) @ policy.map_offsets[1]
        policy.advance()
        self.assertAlmostEqual(policy.map_yaw, .1, places=5)
        np.testing.assert_allclose(policy.map_anchor, anchor, atol=1e-6)

    def test_inertial_bridge_keeps_measured_velocity_during_short_dvl_gap(self):
        policy = self.make_policy()
        sensors = np.zeros(16, np.float32)
        sensors[15] = sensors[11] = 1
        sensors[7] = .5
        sensors[2] = 9.81/20
        image = np.zeros((3,64,84), np.float32)
        for _ in range(5): policy.predict(image, sensors)
        sensors[11] = 0
        sensors[7] = 0
        for _ in range(5): policy.predict(image, sensors)
        self.assertAlmostEqual(policy.position[1], 1, places=4)

    def make_policy(self):
        policy = TrackedGeometry.__new__(TrackedGeometry)
        policy.model = FakeEstimator()
        policy.reset()
        return policy

    def test_classifier_cannot_skip_gates_and_crossing_uses_measured_motion(self):
        policy = self.make_policy()
        sensors = np.zeros(16, np.float32)
        sensors[15] = sensors[11] = 1
        sensors[6] = .3
        image = np.zeros((3, 64, 84), np.float32)
        for _ in range(20):
            policy.predict(image, sensors)
        self.assertEqual(policy.phase, 0)
        sensors[7] = .5
        for _ in range(15):
            policy.predict(image, sensors)
        self.assertEqual(policy.phase, 1)
        policy.reset()
        self.assertEqual(policy.phase, 0)
        self.assertIsNone(policy.goal)

    def test_hover_requires_continuous_sensor_ready_time(self):
        policy = self.make_policy()
        policy.phase = 3
        sensors = np.zeros(16, np.float32)
        sensors[15] = sensors[11] = 1
        sensors[6] = .3
        image = np.zeros((3, 64, 84), np.float32)
        for _ in range(20):
            policy.predict(image, sensors)
        self.assertEqual(policy.phase, 3)
        sensors[11] = 0
        policy.predict(image, sensors)
        self.assertEqual(policy.hold, 0)
        sensors[11] = 1
        for _ in range(39):
            policy.predict(image, sensors)
        self.assertEqual(policy.phase, 4)


if __name__ == '__main__':
    unittest.main()
