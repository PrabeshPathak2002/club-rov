"""Validated OpenCV pinhole calibration, shared by future mission/video consumers."""
from dataclasses import dataclass
import math
from pathlib import Path

import numpy as np
import yaml


@dataclass(frozen=True)
class CameraModel:
    fx: float
    fy: float
    cx: float
    cy: float
    dist: tuple
    size: tuple
    rms_px: float
    medium: str = 'unspecified'

    def __post_init__(self):
        values = (self.fx, self.fy, self.cx, self.cy, self.rms_px, *self.dist)
        if not all(math.isfinite(float(v)) for v in values):
            raise ValueError('Calibration contains nonfinite values')
        if (len(self.size) != 2 or any(isinstance(v, bool) or int(v) != v or v <= 0 for v in self.size)
                or self.fx <= 0 or self.fy <= 0 or self.rms_px < 0):
            raise ValueError('Invalid image size, focal length or RMS error')
        if not (0 <= self.cx < self.size[0] and 0 <= self.cy < self.size[1]):
            raise ValueError('Principal point must be inside the calibrated image')
        if len(self.dist) not in (4, 5, 8, 12, 14):
            raise ValueError('Unsupported OpenCV distortion coefficient count')
        if self.medium not in ('air', 'water', 'unspecified'):
            raise ValueError('Unknown calibration medium')

    @property
    def matrix(self):
        return np.array([[self.fx, 0, self.cx], [0, self.fy, self.cy], [0, 0, 1]], dtype=float)

    @property
    def hfov_deg(self):
        """Pinhole horizontal FOV at the principal row, excluding nonlinear distortion."""
        return math.degrees(math.atan(self.cx / self.fx) + math.atan((self.size[0] - self.cx) / self.fx))

    def scaled(self, size):
        """Resize intrinsics for the same full image; cropping is not supported."""
        if len(size) != 2 or any(isinstance(v, bool) or int(v) != v or v <= 0 for v in size):
            raise ValueError('Target size must contain positive integer width and height')
        sx, sy = size[0] / self.size[0], size[1] / self.size[1]
        # Allow integer rounding of a resized image, but reject a different crop/aspect ratio.
        if abs(size[0] - self.size[0] * sy) > 1.0:
            raise ValueError('Aspect ratio changed; calibrate the actual crop/video mode')
        return CameraModel(self.fx * sx, self.fy * sy, self.cx * sx, self.cy * sy,
                           self.dist, tuple(size), self.rms_px * math.sqrt(sx * sy), self.medium)

    def to_dict(self):
        return dict(schema_version=1, image_size=list(self.size), medium=self.medium,
                    distortion_model='opencv_pinhole',
                    fx=float(self.fx), fy=float(self.fy), cx=float(self.cx), cy=float(self.cy),
                    distortion=list(map(float, self.dist)), rms_px=float(self.rms_px),
                    hfov_deg=self.hfov_deg, hfov_definition='pinhole, principal row; distortion excluded')


def load(path, size=None):
    """Load the calibration tool's JSON/YAML; optionally scale to a full-frame resize.

    Returns CameraModel with fx/fy/cx/cy, dist, size, matrix, rms_px and hfov_deg.
    Distortion coefficients are dimensionless and remain unchanged when resizing.
    """
    try:
        data = yaml.safe_load(Path(path).read_text())
        if not isinstance(data, dict) or data.get('schema_version') != 1:
            raise ValueError('Expected camera calibration schema_version: 1')
        if data.get('distortion_model', 'opencv_pinhole') not in ('opencv_pinhole', 'opencv_pinhole_5'):
            raise ValueError('Expected OpenCV pinhole distortion, not a fisheye/unknown model')
        model = CameraModel(*(float(data[key]) for key in ('fx', 'fy', 'cx', 'cy')),
                            tuple(float(v) for v in data['distortion']), tuple(data['image_size']),
                            float(data['rms_px']), data.get('medium', 'unspecified'))
        return model if size is None else model.scaled(size)
    except (KeyError, TypeError, OverflowError, yaml.YAMLError) as error:
        raise ValueError(f'Invalid camera calibration: {error}') from error
