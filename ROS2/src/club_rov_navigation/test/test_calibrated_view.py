import json
import math

import numpy as np
import pytest

from club_rov_navigation.calibrated_view import CalibratedView, detector_input, open_view
from club_rov_navigation.camera_model import CameraModel

SIZE = (1632, 1232)
FX = 816/math.tan(math.radians(48.7/2))


def model(k1=-0.25, medium='water'):
    return CameraModel(FX, FX, 816.0, 616.0, (k1, 0.0, 0.0, 0.0, 0.0), SIZE, 0.3, medium)


def bar_at(angle_deg, k1):
    """Full frame with a vertical bar where a lens with radial distortion k1 images the given bearing."""
    x = math.tan(math.radians(angle_deg))
    u = round(816 + FX*x*(1 + k1*x*x))
    rgb = np.zeros((SIZE[1], SIZE[0], 3), np.uint8)
    rgb[:, u-6:u+7] = 255
    return rgb


def column(image):
    profile = image[image.shape[0]//2, :, 0].astype(float)
    return float(np.average(np.arange(len(profile)), weights=profile))


def test_bearing_is_corrected_for_distortion():
    rgb = bar_at(22, -0.25)
    small, fx, cx = CalibratedView(model())(rgb)
    assert small.shape[1] == 320
    assert math.degrees(math.atan2(column(small)-cx, fx)) == pytest.approx(22, abs=.3)
    naive, nfx, ncx = detector_input(rgb, None, FX, 816.0)
    assert abs(math.degrees(math.atan2(column(naive)-ncx, nfx)) - 22) > .7


def test_full_frame_intrinsics_and_resized_input():
    view = CalibratedView(model())
    assert view.full_frame(np.zeros((616, 816, 3), np.uint8)) == pytest.approx((FX/2, 408))
    small, fx, _ = view(np.zeros((616, 816, 3), np.uint8))
    assert small.shape[:2] == (242, 320) and fx == pytest.approx(FX*320/1632)


def test_cropped_video_mode_is_refused():
    with pytest.raises(ValueError):
        CalibratedView(model())(np.zeros((720, 1280, 3), np.uint8))


def test_mission_requires_underwater_calibration(tmp_path):
    path = tmp_path/'air.json'
    path.write_text(json.dumps(model(medium='air').to_dict()))
    assert open_view(None) is None
    assert open_view(path) is not None
    with pytest.raises(ValueError, match='underwater'):
        open_view(path, require_water=True)
