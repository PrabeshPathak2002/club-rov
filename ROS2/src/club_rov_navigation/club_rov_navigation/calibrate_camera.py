"""Select diverse checkerboard views and fit an OpenCV pinhole/distortion model.

No MAVLink connection is opened. Calibration must use the actual camera mode,
focus and port, underwater for an underwater mission. RMS alone is not proof
that a flat-port refractive camera is accurately modelled at every distance.
"""
import argparse
from collections import Counter
from datetime import datetime, timezone
import json
import math
import os
from pathlib import Path
import time

import cv2
import numpy as np
import yaml

from .camera_model import CameraModel


def board_points(board, square):
    if len(board) != 2 or min(board) < 3 or not math.isfinite(square) or square <= 0:
        raise ValueError('Board requires at least 3x3 inner corners and positive square size in metres')
    points = np.zeros((board[0] * board[1], 3), np.float32)
    points[:, :2] = np.mgrid[:board[0], :board[1]].T.reshape(-1, 2) * square
    return points


class ViewSelector:
    """Reject blur, small/edge-clipped boards and near-duplicate views.

    Diversity uses the whole normalized corner grid, so changes of position,
    size, rotation and perspective all contribute. Coverage is a 4x3 image grid.
    """
    def __init__(self, board=(9, 6), min_sharpness=60.0, diversity=0.035):
        self.board = tuple(board)
        self.min_sharpness, self.diversity = min_sharpness, diversity
        self.size = None
        self.views, self.features, self.sharpness = [], [], []
        self.covered = set()
        self.rejections = Counter()

    def consider(self, rgb):
        gray = cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY)
        size = (gray.shape[1], gray.shape[0])
        if self.size is not None and size != self.size:
            raise ValueError(f'Image resolution changed from {self.size} to {size}; calibrate each mode separately')
        ok, corners = cv2.findChessboardCorners(gray, self.board,
                                               cv2.CALIB_CB_ADAPTIVE_THRESH | cv2.CALIB_CB_NORMALIZE_IMAGE)
        if not ok:
            self.rejections['board not found'] += 1
            return False, 'board not found', None
        corners = cv2.cornerSubPix(gray, corners, (5, 5), (-1, -1),
                                  (cv2.TERM_CRITERIA_EPS | cv2.TERM_CRITERIA_MAX_ITER, 40, .001))
        points = corners.reshape(-1, 2)
        x, y, width, height = cv2.boundingRect(points)
        sharpness = float(cv2.Laplacian(gray[y:y+height, x:x+width], cv2.CV_64F).var())
        feature = points / np.array(size)
        area = cv2.contourArea(cv2.convexHull(points)) / (size[0] * size[1])
        reason = None
        if sharpness < self.min_sharpness:
            reason = 'blurred'
        elif area < .015 or np.any(feature < .015) or np.any(feature > .985):
            reason = 'too small or near image edge'
        elif any(min(np.sqrt(np.mean(np.sum((feature - old) ** 2, axis=1))),
                     np.sqrt(np.mean(np.sum((feature[::-1] - old) ** 2, axis=1)))) < self.diversity
                 for old in self.features):
            reason = 'duplicate view'
        if reason:
            self.rejections[reason] += 1
            return False, reason, corners
        self.size = size
        self.views.append(corners.copy())
        self.features.append(feature)
        self.sharpness.append(sharpness)
        self.covered.update((min(3, int(px * 4)), min(2, int(py * 3))) for px, py in feature)
        return True, 'accepted', corners

    def coverage(self):
        return '/'.join(''.join('#' if (x, y) in self.covered else '.' for x in range(4)) for y in range(3))

    def geometry_quality(self):
        """Require changes in perspective on both axes and in board scale."""
        if len(self.views) < 2:
            return False, 'vary board distance and tilt in both directions'
        perspective, areas = [], []
        for corners in self.views:
            grid = corners.reshape(self.board[1], self.board[0], 2)
            top = np.linalg.norm(grid[0, -1] - grid[0, 0])
            bottom = np.linalg.norm(grid[-1, -1] - grid[-1, 0])
            left = np.linalg.norm(grid[-1, 0] - grid[0, 0])
            right = np.linalg.norm(grid[-1, -1] - grid[0, -1])
            perspective.append([math.log(top / bottom), math.log(left / right)])
            areas.append(cv2.contourArea(cv2.convexHull(corners)))
        spread = np.ptp(perspective, axis=0)
        ratio = max(areas) / min(areas)
        ok = min(spread) >= .05 and ratio >= 1.25
        return ok, f'tilt spread {spread[0]:.2f}/{spread[1]:.2f}, board area ratio {ratio:.2f}'


def calibrate(views, size, board=(9, 6), square=.025):
    """Fit five distortion coefficients and retain independent per-view RMS errors."""
    if len(views) < 10:
        raise ValueError('Need at least 10 distinct views; collect 20-30 tilted, well-spread views')
    objects = board_points(board, square)
    rms, matrix, dist, rotations, translations = cv2.calibrateCamera(
        [objects.copy() for _ in views], views, size, None, None)
    model = CameraModel(matrix[0, 0], matrix[1, 1], matrix[0, 2], matrix[1, 2],
                        tuple(dist.ravel()), tuple(size), rms)
    errors = []
    for corners, rotation, translation in zip(views, rotations, translations):
        projected, _ = cv2.projectPoints(objects, rotation, translation, matrix, dist)
        errors.append(float(np.sqrt(np.mean(np.sum((corners - projected) ** 2, axis=2)))))
    return model, errors


def offline_frames(path):
    """Visit every offline image/video frame once; EOF is finite, unlike live sources."""
    path = Path(path)
    if path.is_dir():
        folder = path / 'frames' if (path / 'frames').is_dir() else path
        images = sorted(p for p in folder.iterdir() if p.suffix.lower() in ('.jpg', '.jpeg', '.png', '.bmp', '.tif', '.tiff'))
        if not images:
            raise ValueError(f'No images in {folder}')
        for image in images:
            bgr = cv2.imread(str(image))
            if bgr is None:
                raise ValueError(f'Cannot decode image: {image.name}')
            yield image.name, cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
    else:
        capture = cv2.VideoCapture(str(path))
        try:
            if not capture.isOpened():
                raise ValueError(f'Cannot open video file: {path}')
            index = 0
            while True:
                ok, bgr = capture.read()
                if not ok:
                    break
                yield str(index), cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
                index += 1
        finally:
            capture.release()


def live_frames(options):
    # Reuse the recorder's sources, without creating its Telemetry/MAVLink reader.
    from .prequal_record import TopicSource, VideoSource
    source = VideoSource(options.video) if options.video else TopicSource(options.image_topic, options.info_topic)
    started, last_received, last_stamp = time.monotonic(), time.monotonic(), None
    try:
        while time.monotonic() - started < options.duration:
            frame, stamp = source.latest()
            if frame is not None and stamp != last_stamp:
                last_received, last_stamp = time.monotonic(), stamp
                yield str(stamp), frame
            elif time.monotonic() - last_received > options.idle_timeout:
                raise ValueError('Camera stopped providing frames; check the source and retry')
            time.sleep(.02)
    finally:
        source.close()


def is_offline_source(source):
    if source is None or '!' in source or '://' in source:
        return False
    try:
        path = Path(source)
        return path.is_dir() or path.is_file()
    except OSError:
        return False


def main(args=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--video', help='Recorder-compatible device index, RTSP/GStreamer source, video file or image folder')
    parser.add_argument('--image-topic', default='/mapping/rgb/image')
    parser.add_argument('--info-topic', default='/mapping/rgb/camera_info')
    parser.add_argument('--board', default='9x6', help='inner corners, columns x rows (9x6 means 10x7 squares)')
    parser.add_argument('--square', type=float, default=.025, help='measured square side in metres')
    parser.add_argument('--views', type=int, default=30, help='maximum automatically selected views')
    parser.add_argument('--min-views', type=int, default=15)
    parser.add_argument('--min-sharpness', type=float, default=60, help='Laplacian variance in the board region')
    parser.add_argument('--diversity', type=float, default=.035, help='minimum normalized corner-grid displacement')
    parser.add_argument('--duration', type=float, default=180, help='live capture limit in seconds')
    parser.add_argument('--idle-timeout', type=float, default=10)
    parser.add_argument('--preview', action='store_true', help='show corners/coverage; q finishes capture')
    parser.add_argument('--medium', choices=['air', 'water'], required=True, help='labels the actual capture medium')
    parser.add_argument('--out', help='output directory; writes calibration.yaml/.json and accepted frames')
    options = parser.parse_args(args)
    try:
        board = tuple(int(v) for v in options.board.lower().split('x'))
        board_points(board, options.square)
        if not (10 <= options.min_views <= options.views and all(math.isfinite(v) and v > 0 for v in
                (options.duration, options.idle_timeout, options.diversity)) and
                math.isfinite(options.min_sharpness) and options.min_sharpness >= 0):
            raise ValueError('Require 10 <= min-views <= views, positive finite timing/diversity and nonnegative sharpness')
    except (ValueError, TypeError) as error:
        parser.error(str(error))
    if options.preview and os.name != 'nt' and not (os.environ.get('DISPLAY') or os.environ.get('WAYLAND_DISPLAY')):
        parser.error('--preview needs a display; omit it for headless capture')
    workspace = Path(os.environ.get('CLUB_ROV_ROS_WS', str(Path.home() / 'club-rov-ros2-ws')))
    folder = Path(options.out) if options.out else workspace / 'reports' / ('calibration-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ'))
    if folder.exists() and (not folder.is_dir() or any(folder.iterdir())):
        parser.error('Output directory is not empty; choose a new directory')
    (folder / 'frames').mkdir(parents=True, exist_ok=True)
    selector = ViewSelector(board, options.min_sharpness, options.diversity)
    offline = is_offline_source(options.video)
    frames = offline_frames(options.video) if offline else live_frames(options)
    names = []
    print('Move/tilt the flat board across the image and vary distance. Coverage: 4x3 cells (# seen).', flush=True)
    try:
        try:
            for name, rgb in frames:
                accepted, reason, corners = selector.consider(rgb)
                if accepted:
                    target = f'{len(names):03d}.png'
                    if not cv2.imwrite(str(folder / 'frames' / target), cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR)):
                        raise ValueError('Failed to save selected frame')
                    names.append(dict(file=target, source_frame=name))
                    print(f'{len(names)}/{options.views} views | coverage {selector.coverage()} | '
                          f'{selector.geometry_quality()[1]} | sharpness {selector.sharpness[-1]:.0f}', flush=True)
                if options.preview:
                    preview = cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR)
                    if corners is not None:
                        cv2.drawChessboardCorners(preview, board, corners, True)
                    cv2.putText(preview, f'{len(names)} views {selector.coverage()} {reason}', (10, 25),
                                cv2.FONT_HERSHEY_SIMPLEX, .55, (0, 255, 0), 1)
                    cv2.imshow('Camera calibration (q to finish)', preview)
                    if cv2.waitKey(1) & 255 == ord('q'):
                        break
                if len(names) >= options.views:
                    break
        except KeyboardInterrupt:
            print('Capture stopped; fitting selected views.', flush=True)
        if len(names) < options.min_views or len(selector.covered) < 6:
            raise ValueError(f'Insufficient diverse coverage: {len(names)} views, {len(selector.covered)}/12 cells; selected frames preserved')
        good_geometry, detail = selector.geometry_quality()
        if not good_geometry:
            raise ValueError('Insufficient tilt/scale diversity: ' + detail + '; selected frames preserved')
        model, errors = calibrate(selector.views, selector.size, board, options.square)
        result = model.to_dict()
        result.update(medium=options.medium, board_inner_corners=list(board), square_size_m=options.square,
                      created_utc=datetime.now(timezone.utc).isoformat(), view_count=len(names),
                      coverage=selector.coverage(), covered_cells=len(selector.covered),
                      per_view_rms_px=errors, accepted_frames=names, rejected=dict(selector.rejections),
                      distortion_model='opencv_pinhole_5',
                      warnings=['Flat-port refraction is approximated by a pinhole/distortion model; validate at mission distances.'] +
                      (['RMS exceeds 1 pixel; inspect corner detections and repeat capture.'] if model.rms_px > 1 else []))
        (folder / 'calibration.json').write_text(json.dumps(result, indent=2, allow_nan=False) + '\n')
        (folder / 'calibration.yaml').write_text(yaml.safe_dump(result, sort_keys=False))
        print(f'Saved {folder}: RMS {model.rms_px:.3f} px; pinhole HFOV {model.hfov_deg:.2f} deg at {model.size[0]}x{model.size[1]}', flush=True)
        for warning in result['warnings']:
            print('NOTE: ' + warning, flush=True)
        return 0
    except (ValueError, RuntimeError, cv2.error, OSError) as error:
        print(f'Calibration failed: {error}', flush=True)
        return 1
    finally:
        frames.close()
        if options.preview:
            cv2.destroyAllWindows()


if __name__ == '__main__':
    raise SystemExit(main())
