import unittest
import numpy as np
from marker_measurements import gate_measurement, hover_measurements, dock_bearing


class MarkerMeasurementTests(unittest.TestCase):
    def test_cyan_ring_recovers_metric_center_from_known_diameter(self):
        image = np.ones((3, 64, 84), np.float32)
        image[0] = .8
        y, x = np.mgrid[:64, :84]
        ring = np.abs(np.sqrt((x-41.5)**2+(y-33.7)**2)-24.7) < .8
        image[:, ring] = np.array([.35, .6, .68])[:, None]
        measurements = hover_measurements(image)
        self.assertTrue(measurements)
        closest = min(measurements, key=lambda p: abs(p[2]-2))
        self.assertAlmostEqual(closest[2], 2, delta=.1)
        self.assertAlmostEqual(closest[0], 0, delta=.03)
        self.assertAlmostEqual(closest[1], 0, delta=.03)

    def test_dock_bearing_rejects_clipped_green_target(self):
        image = np.ones((3,64,84), np.float32)
        image[0] = .8
        image[:,20:40,50:60] = np.array([.1,.8,.2])[:,None,None]
        self.assertGreater(dock_bearing(image), .1)
        image[:,20:40,:2] = np.array([.1,.8,.2])[:,None,None]
        self.assertIsNone(dock_bearing(image))

    def test_full_frame_recovers_center_and_metric_range(self):
        image = np.zeros((3, 64, 84), np.float32)
        image[0:2, 7:57, 17:19] = 1
        image[0:2, 7:57, 65:67] = 1
        result = gate_measurement(image, 3.31, 1)
        self.assertIsNotNone(result)
        self.assertAlmostEqual(result[0], 0, delta=.05)
        self.assertAlmostEqual(result[1], .04, delta=.05)
        self.assertAlmostEqual(result[2], 3.31, delta=.2)

    def test_single_post_uses_approach_side_and_rejects_distant_background(self):
        image = np.zeros((3, 64, 84), np.float32)
        image[0:2, 7:57, 50:52] = 1
        left = gate_measurement(image, 3.31, 1)
        right = gate_measurement(image, 3.31, -1)
        self.assertAlmostEqual(left[0]-right[0], 1.6, places=5)
        image[:] = 0
        image[0:2, 20:40, 50:52] = 1
        self.assertIsNone(gate_measurement(image, 3.31, 1))


if __name__ == '__main__': unittest.main()
