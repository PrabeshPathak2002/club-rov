import json
import math

import cv2
import numpy as np
import pytest

from club_rov_navigation.marker_detector import (PI_CAMERA_V2_FLAT_PORT_HFOV, detect_gate, detection_scale,
                                                 focal_length, underwater_fov)
from club_rov_navigation.prequal_review import main as review


def gate_frame(width=320, height=180):
    image = np.full((height, width, 3), (20, 90, 110), np.uint8)
    post, left, right, bottom = max(1, width//80), width*100//320, width*240//320, height*130//180
    image[0:bottom, left:left+post] = image[0:bottom, right:right+post] = (200, 150, 60)
    image[bottom-post:bottom, left:right+post] = (200, 150, 60)
    return image


def test_focal_length_from_field_of_view():
    assert focal_length(320, 90) == pytest.approx(160)
    with pytest.raises(ValueError):
        focal_length(320, 0)


def test_flat_port_narrows_the_pi_camera_view():
    assert underwater_fov(66.0) == pytest.approx(52.0, abs=.2)
    assert PI_CAMERA_V2_FLAT_PORT_HFOV == pytest.approx(48.7, abs=.2)
    assert underwater_fov(66.0, refractive_index=1.0) == pytest.approx(66.0)


def test_large_frames_are_detected_at_the_tuned_scale():
    small = detect_gate(gate_frame(), focal_length(320, 80))
    big, fx, cx = detection_scale(gate_frame(1280, 720), focal_length(1280, 80))
    assert big.shape[1] == 320 and fx == pytest.approx(focal_length(320, 80)) and cx == pytest.approx(160)
    gate = detect_gate(big, fx, cx)
    assert gate.bearing == pytest.approx(small.bearing, abs=math.radians(1))
    assert gate.angular_width == pytest.approx(small.angular_width, rel=.05)


def test_review_annotates_a_recording(tmp_path, capsys):
    (tmp_path/'frames').mkdir()
    cv2.imwrite(str(tmp_path/'frames'/'000000.jpg'), cv2.cvtColor(gate_frame(1280, 720), cv2.COLOR_RGB2BGR))
    entry = dict(file='000000.jpg', fx=focal_length(1280, 80), cx=None, depth_m=1.2, heading_deg=10)
    (tmp_path/'log.jsonl').write_text(json.dumps(entry)+'\n')
    review([str(tmp_path)])
    out = capsys.readouterr().out
    assert 'depth=1.20' in out and 'gate +' in out and 'gate in 1' in out
    assert (tmp_path/'review'/'000000.png').exists()
