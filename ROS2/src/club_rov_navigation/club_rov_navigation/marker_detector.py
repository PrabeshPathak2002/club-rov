"""Colour-threshold detector for the vertical prequalification marker."""
import math
from dataclasses import dataclass

import cv2
import numpy as np

from club_rov_navigation.prequal_plan import Marker


@dataclass(frozen=True)
class MarkerColour:
    """OpenCV HSV limits; hue is 0-180. Defaults match a red-orange pole and exclude the gold gate (hue 20-24)."""
    hue_low: int = 0
    hue_high: int = 15
    saturation_min: int = 110
    value_min: int = 70
    min_pixels: int = 12
    min_aspect: float = 2.0


# Water haze shifts the far side of the gold gate toward green (hue ~45-50, saturation ~50); the water is ~96.
GATE_COLOUR = MarkerColour(hue_low=17, hue_high=60, saturation_min=40, value_min=45, min_pixels=30)


def colour_mask(rgb, fx, colour):
    if rgb.ndim != 3 or rgb.shape[2] != 3 or rgb.dtype != np.uint8:
        raise ValueError('Expected an 8-bit RGB image')
    if not math.isfinite(fx) or fx <= 0:
        raise ValueError('Invalid focal length')
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)
    return cv2.inRange(hsv, (colour.hue_low, colour.saturation_min, colour.value_min), (colour.hue_high, 255, 255))


def components(rgb, fx, colour):
    count, labels, stats, _ = cv2.connectedComponentsWithStats(colour_mask(rgb, fx, colour), connectivity=8)
    return count, labels, stats


def detect_gate(rgb, fx, cx=None, colour=GATE_COLOUR):
    """Bearing to the gate centre and its angular width, only when both posts are in view.

    Posts are found column by column rather than as one connected outline, because haze often
    breaks the faint side of the gate away from the bar. Two posts of similar height that end on
    a common bottom line form the gate; a single post, bar fragments and solid blobs are rejected.
    """
    mask = colour_mask(rgb, fx, colour) > 0
    image_height, image_width = mask.shape
    tall = mask.sum(axis=0) >= max(20, .2*image_height)
    edges = np.flatnonzero(np.diff(np.concatenate(([0], tall.astype(np.int8), [0]))))
    posts = []
    for start, end in zip(edges[::2], edges[1::2]):
        if start <= 1 or end >= image_width-1:
            continue
        rows = np.flatnonzero(mask[:, start:end].any(axis=1))
        posts.append((start, end, rows[0], rows[-1]))
    best = None
    for i, (a_start, a_end, a_top, a_bottom) in enumerate(posts):
        for b_start, b_end, b_top, b_bottom in posts[i+1:]:
            span = b_end-a_start
            heights = a_bottom-a_top+1, b_bottom-b_top+1
            if span < 16 or max(a_end-a_start, b_end-b_start) > .25*span:
                continue
            if min(heights) < .4*max(heights) or abs(a_bottom-b_bottom) > .25*max(heights):
                continue
            if best is None or span > best[1]:
                best = (a_start, span)
    if best is None:
        return None
    left, width = best
    centre = rgb.shape[1]/2 if cx is None else cx
    return Marker(bearing=math.atan2(left+width/2-centre, fx),
                  angular_width=math.atan2(left+width-centre, fx)-math.atan2(left-centre, fx))


def detect_marker(rgb, fx, cx=None, colour=MarkerColour()):
    """Return the tallest-and-thinnest matching blob as a Marker, or None."""
    count, _, stats = components(rgb, fx, colour)
    best = None
    for left, top, width, height, area in stats[1:count]:
        if area < colour.min_pixels or height < colour.min_aspect*width:
            continue
        if best is None or area > best[4]:
            best = (left, top, width, height, area)
    if best is None:
        return None
    left, _, width, _, _ = best
    centre = rgb.shape[1]/2 if cx is None else cx
    return Marker(bearing=math.atan2(left+width/2-centre, fx), angular_width=2*math.atan2(width/2, fx))


def detection_scale(rgb, fx, cx=None, width=320):
    """Shrink a frame to the width the pixel thresholds were tuned at; returns (image, fx, cx)."""
    centre = rgb.shape[1]/2 if cx is None else cx
    if rgb.shape[1] <= width:
        return rgb, fx, centre
    scale = width/rgb.shape[1]
    small = cv2.resize(rgb, (width, round(rgb.shape[0]*scale)), interpolation=cv2.INTER_AREA)
    return small, fx*scale, centre*scale


def underwater_fov(air_fov_deg, refractive_index=1.333):
    """Field of view behind a flat port; refraction at the port narrows it (a dome port does not)."""
    return 2*math.degrees(math.atan(math.tan(math.radians(air_fov_deg)/2)/refractive_index))


# Pi Camera V2.1 (IMX219): 62.2 degrees horizontal in air, about 48.7 underwater behind a flat port.
# Valid for the ESP32's full-width 1632 x 1232 binned mode; cropped modes narrow it further.
PI_CAMERA_V2_FLAT_PORT_HFOV = round(underwater_fov(62.2), 1)


def focal_length(image_width, horizontal_fov_deg):
    if not 0 < horizontal_fov_deg < 180:
        raise ValueError('Horizontal field of view must be between 0 and 180 degrees')
    return image_width/2/math.tan(math.radians(horizontal_fov_deg)/2)
