"""Re-run the prequal detectors over recorded frames to tune colour thresholds.

Works on a `prequal_record` folder (uses its log.jsonl for focal length) or any folder of
images. Writes annotated copies to <folder>/review/ and prints one line per frame: the marker
mask is tinted red, the gate mask yellow, and detections are drawn as lines and a box.
"""
import argparse
import json
import math
import sys
from pathlib import Path

import cv2
import numpy as np
from .calibrated_view import detector_input, open_view
from .marker_detector import (GATE_COLOUR, PI_CAMERA_V2_FLAT_PORT_HFOV, MarkerColour, colour_mask, detect_gate,
                              detect_marker)


def annotate(rgb, fx, cx, marker, gate, marker_colour, gate_colour):
    out = rgb.copy()
    out[colour_mask(rgb, fx, marker_colour) > 0] = (255, 0, 0)
    out[colour_mask(rgb, fx, gate_colour) > 0] = (255, 255, 0)
    height = rgb.shape[0]
    if marker is not None:
        x = round(cx+fx*math.tan(marker.bearing))
        cv2.line(out, (x, 0), (x, height-1), (255, 0, 255), 1)
    if gate is not None:
        left = round(cx+fx*math.tan(gate.bearing-gate.angular_width/2))
        right = round(cx+fx*math.tan(gate.bearing+gate.angular_width/2))
        cv2.rectangle(out, (left, 1), (right, height-2), (0, 255, 0), 1)
    return out


def main(args=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('folder', help='recording folder, or a folder of .jpg/.png images')
    parser.add_argument('--hfov', type=float, default=PI_CAMERA_V2_FLAT_PORT_HFOV,
                        help='horizontal field of view when the log has no fx')
    parser.add_argument('--hue', type=int, nargs=2, default=(0, 15))
    parser.add_argument('--saturation', type=int, default=MarkerColour.saturation_min, help='marker minimum saturation')
    parser.add_argument('--value', type=int, default=MarkerColour.value_min, help='marker minimum brightness')
    parser.add_argument('--gate-hue', type=int, nargs=2, default=(GATE_COLOUR.hue_low, GATE_COLOUR.hue_high))
    parser.add_argument('--gate-saturation', type=int, default=GATE_COLOUR.saturation_min)
    parser.add_argument('--gate-value', type=int, default=GATE_COLOUR.value_min)
    parser.add_argument('--marker-diameter', type=float, default=.16, help='metres, for distance estimates')
    parser.add_argument('--gate-width', type=float, default=2.0, help='metres, for distance estimates')
    parser.add_argument('--calibration', help='calibrate_camera output; overrides --hfov and logged fx, '
                                              'and removes lens distortion (review images are undistorted)')
    options = parser.parse_args(args)
    try:
        view = open_view(options.calibration)
    except (OSError, ValueError) as error:
        parser.error(str(error))

    folder = Path(options.folder)
    images = folder/'frames' if (folder/'frames').is_dir() else folder
    log = {}
    if (folder/'log.jsonl').exists():
        for line in (folder/'log.jsonl').read_text().splitlines():
            entry = json.loads(line)
            log[entry['file']] = entry
    paths = sorted(p for p in images.iterdir() if p.suffix.lower() in ('.jpg', '.jpeg', '.png'))
    if not paths:
        sys.exit(f'No images in {images}')
    marker_colour = MarkerColour(options.hue[0], options.hue[1], options.saturation, options.value)
    gate_colour = MarkerColour(options.gate_hue[0], options.gate_hue[1], options.gate_saturation,
                               options.gate_value, GATE_COLOUR.min_pixels)
    review = folder/'review'
    review.mkdir(exist_ok=True)
    markers = gates = 0
    for path in paths:
        rgb = cv2.cvtColor(cv2.imread(str(path)), cv2.COLOR_BGR2RGB)
        entry = log.get(path.name, {})
        small, small_fx, small_cx = detector_input(rgb, view, entry.get('fx'), entry.get('cx'), options.hfov)
        marker = detect_marker(small, small_fx, small_cx, marker_colour)
        gate = detect_gate(small, small_fx, small_cx, gate_colour)
        markers, gates = markers+(marker is not None), gates+(gate is not None)
        cv2.imwrite(str(review/(path.stem+'.png')), cv2.cvtColor(
            annotate(small, small_fx, small_cx, marker, gate, marker_colour, gate_colour), cv2.COLOR_RGB2BGR))
        depth, heading = entry.get('depth_m'), entry.get('heading_deg')
        hsv = cv2.cvtColor(small, cv2.COLOR_RGB2HSV).reshape(-1, 3)
        print(f'{path.name}'
              + (f' depth={depth:.2f}' if depth is not None else '')
              + (f' heading={heading:.0f}' if heading is not None else '')
              + (f' marker {math.degrees(marker.bearing):+.1f}° {marker.distance(options.marker_diameter):.1f}m'
                 if marker else ' marker -')
              + (f' gate {math.degrees(gate.bearing):+.1f}° {gate.distance(options.gate_width):.1f}m' if gate else ' gate -')
              + f' water hsv={np.median(hsv, axis=0).astype(int).tolist()}')
    print(f'{len(paths)} frames: marker in {markers}, gate in {gates}. Annotated copies in {review}')
