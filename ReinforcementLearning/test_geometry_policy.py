import unittest
import torch
from geometry_policy import GeometryPolicy, WorldGeometryPolicy


class GeometryPolicyTests(unittest.TestCase):
    def test_world_landmark_query_uses_analytic_measured_pose(self):
        class ConstantMap(torch.nn.Module):
            def forward(self, encoded):
                values = torch.zeros(*encoded.shape[:2], 5, 5)
                values[:, :, :, 0] = 1  # map X = 3 m
                values[:, :, :, 4] = 1  # map heading zero
                return values.flatten(2)
        model = WorldGeometryPolicy().eval()
        model.state = ConstantMap()
        memory = torch.zeros(1, 1, 136)
        memory[0, 0, 128] = 1  # measured X = 1 m
        sensor = torch.zeros(1, 16)
        sensor[0, 6] = .25  # pressure depth 1 m
        sensor[0, 15] = 1
        with torch.no_grad():
            _, _, estimate, _ = model(torch.zeros(1, 3, 64, 84), sensor, memory,
                                     torch.tensor([[1., 0, 0, 0, 0]]))
        torch.testing.assert_close(estimate[0, :3], torch.tensor([2., 1., 0.]))

    def test_streaming_memory_matches_sequence_and_integrates_measured_odometry(self):
        torch.set_num_threads(1)
        torch.manual_seed(1)
        model = GeometryPolicy().eval()
        images = torch.rand(1, 3, 3, 64, 84)
        sensors = torch.zeros(1, 3, 16)
        sensors[:, :, 7] = .5  # Measured forward speed 1 m/s.
        sensors[:, :, 11] = sensors[:, :, 15] = 1
        initial = torch.zeros(1, 1, 136)
        with torch.no_grad():
            state, phase, final = model.estimate(images, sensors, initial)
            memory = initial
            estimates = []
            for i in range(3):
                _, memory, estimate, _ = model(images[:, i], sensors[:, i], memory)
                estimates.append(estimate)
        torch.testing.assert_close(torch.stack(estimates, 1), state, atol=1e-6, rtol=1e-5)
        torch.testing.assert_close(memory, final, atol=1e-6, rtol=1e-5)
        self.assertAlmostEqual(final[0, 0, 129].item(), .3, places=6)
        self.assertAlmostEqual(final[0, 0, 128].item(), 0, places=6)
        sensors[:, :, 11] = 0
        with torch.no_grad():
            _, _, dropped = model.estimate(images, sensors, initial)
        self.assertEqual(dropped[0, 0, 129].item(), 0)

    def test_gyro_yaw_uses_flu_sign_and_outputs_bounded_finite_commands(self):
        model = GeometryPolicy().eval()
        sensors = torch.zeros(1, 16)
        sensors[:, 5] = -.5  # Unity yaw rate +1 rad/s.
        sensors[:, 15] = 1
        with torch.no_grad():
            action, memory, _, _ = model(torch.zeros(1, 3, 64, 84), sensors, torch.zeros(1, 1, 136))
        self.assertAlmostEqual(memory[0, 0, 130].item(), .1, places=6)
        self.assertTrue(torch.isfinite(action).all())
        self.assertLessEqual(action.abs().max().item(), .600001)


if __name__ == '__main__':
    unittest.main()
