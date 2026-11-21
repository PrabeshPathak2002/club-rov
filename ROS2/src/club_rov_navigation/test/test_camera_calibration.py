"""Synthetic optical projection tests; no camera, ROS graph or vehicle required."""
import json
import math

import cv2
import numpy as np
import pytest
import yaml

from club_rov_navigation.calibrate_camera import ViewSelector, board_points, calibrate, is_offline_source, main, offline_frames
from club_rov_navigation.camera_model import CameraModel, load


BOARD = (9, 6)
SQUARE = .025
SIZE = (640, 480)
K = np.array([[700., 0, 317.], [0, 710., 243.], [0, 0, 1.]])
DIST = np.array([-.12, .035, .001, -.001, 0.])


def render_board(rotation, translation):
    """Ray-cast a flat checkerboard through known radial/tangential distortion."""
    scale = 2
    y, x = np.indices((SIZE[1] * scale, SIZE[0] * scale), dtype=np.float32)
    pixels = np.stack(((x + .5) / scale - .5, (y + .5) / scale - .5), axis=-1).reshape(-1, 1, 2)
    normalized = cv2.undistortPoints(pixels, K, DIST).reshape(-1, 2)
    rays = np.column_stack((normalized, np.ones(len(normalized))))
    r, _ = cv2.Rodrigues(rotation)
    camera = -r.T @ translation
    directions = rays @ r
    points = camera + directions * (-camera[2] / directions[:, 2, None])
    inside = ((points[:, 0] >= -SQUARE) & (points[:, 0] < BOARD[0] * SQUARE) &
              (points[:, 1] >= -SQUARE) & (points[:, 1] < BOARD[1] * SQUARE))
    cells = np.floor((points[:, :2] + SQUARE) / SQUARE).astype(int)
    image = np.full(len(points), 230, np.uint8)
    image[inside] = np.where(cells[inside].sum(axis=1) % 2, 245, 15)
    image = cv2.resize(image.reshape(SIZE[1] * scale, SIZE[0] * scale), SIZE, interpolation=cv2.INTER_AREA)
    return cv2.cvtColor(image, cv2.COLOR_GRAY2RGB)


@pytest.fixture(scope='module')
def rendered_views():
    rng = np.random.default_rng(812)
    result = []
    center = np.array([.1, .0625, 0])
    for _ in range(22):
        rotation = rng.uniform([-.45, -.45, -.3], [.45, .45, .3])
        r, _ = cv2.Rodrigues(rotation)
        translation = rng.uniform([-.07, -.055, .53], [.07, .055, .8]) - r @ center
        result.append(render_board(rotation, translation))
    return result


def test_rendered_checkerboards_recover_intrinsics_and_distortion(rendered_views):
    selector = ViewSelector(BOARD)
    for image in rendered_views:
        selector.consider(image)
    assert len(selector.views) >= 15
    assert len(selector.covered) >= 8
    assert selector.geometry_quality()[0]
    model, errors = calibrate(selector.views, selector.size, BOARD, SQUARE)
    assert model.fx == pytest.approx(K[0, 0], rel=.015)
    assert model.fy == pytest.approx(K[1, 1], rel=.015)
    assert model.cx == pytest.approx(K[0, 2], abs=3)
    assert model.cy == pytest.approx(K[1, 2], abs=3)
    # Compare distortion's effect on rays rather than unstable higher-order coefficients.
    probe = np.array([[-.3, -.2, 1], [.3, .2, 1], [0, 0, 1]], dtype=float)
    expected, _ = cv2.projectPoints(probe, np.zeros(3), np.zeros(3), K, DIST)
    actual, _ = cv2.projectPoints(probe, np.zeros(3), np.zeros(3), model.matrix, np.array(model.dist))
    assert np.max(np.linalg.norm(expected - actual, axis=2)) < 2
    assert model.rms_px < .5
    assert max(errors) < .6
    expected_hfov = math.degrees(math.atan(317 / 700) + math.atan(323 / 700))
    assert model.hfov_deg == pytest.approx(expected_hfov, abs=.6)


def test_selection_rejects_duplicates_blur_and_wrong_resolution(rendered_views):
    selector = ViewSelector()
    assert selector.consider(rendered_views[0])[0]
    assert selector.consider(rendered_views[0])[1] == 'duplicate view'
    blurred = cv2.GaussianBlur(rendered_views[1], (31, 31), 8)
    assert not selector.consider(blurred)[0]
    assert not selector.consider(np.full_like(rendered_views[0], 128))[0]
    with pytest.raises(ValueError, match='resolution changed'):
        selector.consider(cv2.resize(rendered_views[0], (320, 240)))


@pytest.mark.parametrize('suffix', ['json', 'yaml'])
def test_model_round_trip_scaling_and_hfov(tmp_path, suffix):
    model = CameraModel(700, 710, 317, 243, tuple(DIST), SIZE, .2)
    path = tmp_path / ('camera.' + suffix)
    path.write_text(json.dumps(model.to_dict()) if suffix == 'json' else yaml.safe_dump(model.to_dict()))
    result = load(path, (320, 240))
    assert result.fx == 350 and result.cx == 158.5
    assert result.fy == 355 and result.cy == 121.5
    assert result.dist == model.dist
    assert result.hfov_deg == pytest.approx(model.hfov_deg)
    assert load(path).size == SIZE
    with pytest.raises(ValueError, match='Aspect ratio'):
        load(path, (320, 180))


@pytest.mark.parametrize('change', [{'fx': -1}, {'fy': float('nan')}, {'cx': 900},
                                  {'image_size': [0, 480]}, {'distortion': [0, 0]},
                                  {'schema_version': 2}, {'rms_px': -1}, {'distortion_model': 'fisheye'}])
def test_invalid_calibrations_rejected(tmp_path, change):
    data = CameraModel(700, 710, 317, 243, tuple(DIST), SIZE, .2).to_dict()
    data.update(change)
    path = tmp_path / 'bad.json'
    path.write_text(json.dumps(data))
    with pytest.raises(ValueError):
        load(path)


def test_offline_cli_writes_consistent_outputs_and_preserves_inputs(tmp_path, rendered_views):
    source = tmp_path / 'recording' / 'frames'
    source.mkdir(parents=True)
    for i, image in enumerate(rendered_views):
        assert cv2.imwrite(str(source / f'{i:03d}.png'), cv2.cvtColor(image, cv2.COLOR_RGB2BGR))
    out = tmp_path / 'output'
    assert main(['--video', str(source.parent), '--board', '9x6', '--square', '.025',
                 '--medium', 'water', '--out', str(out)]) == 0
    assert load(out / 'calibration.json') == load(out / 'calibration.yaml')
    assert load(out / 'calibration.json', (320, 240)).medium == 'water'
    data = json.loads((out / 'calibration.json').read_text())
    assert data['medium'] == 'water' and data['view_count'] >= 15
    assert len(data['per_view_rms_px']) == data['view_count']
    assert len(list(source.glob('*.png'))) == len(rendered_views)
    with pytest.raises(SystemExit):
        main(['--video', str(source), '--medium', 'water', '--out', str(out)])


def test_video_eof_and_insufficient_views_fail_without_calibration(tmp_path, rendered_views):
    path = tmp_path / 'board.avi'
    writer = cv2.VideoWriter(str(path), cv2.VideoWriter_fourcc(*'MJPG'), 10, SIZE)
    assert writer.isOpened()
    for image in rendered_views[:3]:
        writer.write(cv2.cvtColor(image, cv2.COLOR_RGB2BGR))
    writer.release()
    assert len(list(offline_frames(path))) == 3
    out = tmp_path / 'failed'
    assert main(['--video', str(path), '--medium', 'air', '--out', str(out)]) == 1
    assert not (out / 'calibration.json').exists()
    with pytest.raises(ValueError, match='10 distinct'):
        calibrate([], SIZE)
    with pytest.raises(ValueError):
        board_points((9, 6), 0)


def test_flat_on_views_are_not_sufficient_despite_image_coverage():
    selector = ViewSelector()
    for x in range(4):
        for y in range(3):
            image = render_board(np.zeros(3), np.array([-.16 + x * .04, -.11 + y * .04, .65]))
            selector.consider(image)
    assert len(selector.views) >= 10
    assert not selector.geometry_quality()[0]


def test_live_source_reuses_recorder_and_closes_on_early_exit(monkeypatch):
    from types import SimpleNamespace
    from club_rov_navigation.calibrate_camera import live_frames
    from club_rov_navigation import prequal_record
    calls = []

    class FakeSource:
        def __init__(self, pipeline):
            calls.append(pipeline)

        def latest(self):
            return np.zeros((20, 20, 3), np.uint8), 1

        def close(self):
            calls.append('closed')

    monkeypatch.setattr(prequal_record, 'VideoSource', FakeSource)
    frames = live_frames(SimpleNamespace(video='test ! appsink', duration=1, idle_timeout=.1))
    assert next(frames)[1].shape == (20, 20, 3)
    frames.close()
    assert calls == ['test ! appsink', 'closed']


def test_long_gstreamer_pipeline_is_not_treated_as_a_file(monkeypatch):
    from club_rov_navigation.prequal_record import VideoSource

    class FakeCapture:
        def isOpened(self):
            return True

        def release(self):
            pass

    monkeypatch.setattr(cv2, 'VideoCapture', lambda *args: FakeCapture())
    monkeypatch.setattr(VideoSource, 'read', lambda self: None)
    source = VideoSource('udpsrc ! ' + 'identity ! ' * 40 + 'appsink')
    assert source.period == 0
    source.close()


def test_offline_detection_excludes_streams_and_device_nodes(tmp_path):
    import os
    assert is_offline_source(str(tmp_path))
    assert not is_offline_source('rtsp://camera/video')
    assert not is_offline_source('source ! ' + 'identity ! ' * 50 + 'appsink')
    assert not is_offline_source('0')
    pipe = tmp_path / 'camera-device'
    os.mkfifo(pipe)
    assert not is_offline_source(str(pipe))
