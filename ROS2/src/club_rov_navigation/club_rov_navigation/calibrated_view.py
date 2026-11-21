"""Detector input from a measured camera calibration: shrink, then remove lens distortion."""
import cv2
import numpy as np

from .camera_model import load
from .marker_detector import detection_scale, focal_length


class CalibratedView:
    """Turns full camera frames into undistorted (image, fx, cx) at the detectors' working width."""

    def __init__(self, model, width=320):
        self.model, self.width, self.maps = model, width, {}

    def __call__(self, rgb):
        h, w = rgb.shape[:2]
        if (w, h) not in self.maps:
            target = (self.width, round(h*self.width/w)) if w > self.width else (w, h)
            m = self.model.scaled(target)
            maps = cv2.initUndistortRectifyMap(m.matrix, np.asarray(m.dist), None, m.matrix, target, cv2.CV_16SC2)
            self.maps[w, h] = target, maps, m.fx, m.cx
        target, maps, fx, cx = self.maps[w, h]
        small = rgb if target == (w, h) else cv2.resize(rgb, target, interpolation=cv2.INTER_AREA)
        return cv2.remap(small, *maps, cv2.INTER_LINEAR), fx, cx

    def full_frame(self, rgb):
        """fx, cx of the calibration at this frame's resolution, for logs."""
        m = self.model.scaled((rgb.shape[1], rgb.shape[0]))
        return m.fx, m.cx


def open_view(path, require_water=False):
    """None without a path; raises ValueError for an unusable calibration."""
    if not path:
        return None
    model = load(path)
    if require_water and model.medium != 'water':
        raise ValueError(f'{path} was calibrated in {model.medium}; the mission needs an underwater calibration '
                         'taken behind the actual port')
    return CalibratedView(model)


def detector_input(rgb, view, fx=None, cx=None, hfov=None):
    """(image, fx, cx) for the detectors: calibrated when a view is given, else the pinhole estimate."""
    if view is not None:
        return view(rgb)
    return detection_scale(rgb, fx or focal_length(rgb.shape[1], hfov), cx)
