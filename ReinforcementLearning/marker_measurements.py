"""Camera-only gate measurements using calibrated FOV and known frame dimensions."""
import math
import numpy as np
try:
    import cv2
except ImportError:
    import sys
    sys.path.append('/usr/lib/python3/dist-packages')
    import cv2


FOCAL = 42 / math.tan(math.radians(48.7 / 2))


def hover_measurements(image):
    r, g, b = image
    mask = ((b > r*1.65) & (g > r*1.4) & (g < .78) & (b > g*1.05)).astype(np.uint8)*255
    contours, _ = cv2.findContours(mask, cv2.RETR_LIST, cv2.CHAIN_APPROX_NONE)
    measurements = []
    for contour in contours:
        if len(contour) < 15: continue
        (x, y), (w, h), angle = cv2.fitEllipse(contour)
        if min(w, h) < 8 or max(w, h)/min(w, h) > 1.25: continue
        if not .75 < cv2.contourArea(contour)/(math.pi*w*h/4) < 1.2: continue
        if x-w/2 < 1 or x+w/2 > 82 or y-h/2 < 1 or y+h/2 > 62: continue
        z = FOCAL*.9/((w+h)/2)
        measurements.append(np.array([(x-41.5)*z/FOCAL, .04+(31.5-y)*z/FOCAL, z+.31]))
    return measurements


def dock_bearing(image):
    r, g, b = image
    mask = (g > r*1.25) & (g > b*1.12)
    ys, xs = np.nonzero(mask)
    if len(xs) < 30 or xs.min() < 2 or xs.max() > 81 or ys.min() < 2 or ys.max() > 61:
        return None
    return math.atan2((xs.min()+xs.max())/2-41.5, FOCAL)


def gate_measurement(image, expected_forward, approach_side, expected_lateral=None):
    r, g, b = image
    mask = (r > .3) & (g > .25) & (r > b*1.3) & (g > b*1.3)
    candidates = []
    columns = []
    for x in range(84):
        rows = np.flatnonzero(mask[:, x])
        if len(rows) >= 12 and len(rows)/(rows[-1]-rows[0]+1) > .65:
            columns.append(x)
    groups = np.split(columns, np.flatnonzero(np.diff(columns) > 1)+1) if columns else []
    for group in groups:
        if not len(group) or len(group) > 15: continue
        rows = np.flatnonzero(mask[:, group].any(1))
        lo, hi = int(rows[0]), int(rows[-1])
        height = hi-lo+1
        if height < len(group)*2: continue
        full = lo > 0 and hi < 63
        z = FOCAL*1.57/height if full else max(.08, expected_forward-.31)
        if full and abs(z+.31-expected_forward) > max(1., expected_forward*.35): continue
        candidates.append(dict(x=float(np.mean(group)), lo=lo, hi=hi, z=z, height=height, full=full))
    if not candidates: return None
    candidates.sort(key=lambda c: c['height'], reverse=True)
    first = candidates[0]
    pair = next((c for c in candidates[1:] if abs(c['x']-first['x']) > 8 and
                 abs(c['height']-first['height']) < first['height']*.2), None)
    if pair is not None:
        z = FOCAL*1.6/abs(pair['x']-first['x'])
        center_x = (pair['x']+first['x'])/2
        x = (center_x-41.5)*z/FOCAL
    else:
        z = first['z']
        # Approaching from port first reveals the port post, and vice versa.
        post_x = (first['x']-41.5)*z/FOCAL
        if expected_lateral is None:
            x = post_x + (.8 if approach_side >= 0 else -.8)
        else:
            x = min((post_x+.8, post_x-.8), key=lambda value: abs(value-expected_lateral))
    if abs(z+.31-expected_forward) > max(1., expected_forward*.35): return None
    lo, hi = first['lo'], first['hi']
    if lo > 0 and hi < 63:
        y = .04 + (31.5-(lo+hi)/2)*z/FOCAL
    elif lo == 0 and hi < 63:
        y = .04 + (31.5-hi)*z/FOCAL + .785
    elif hi == 63 and lo > 0:
        y = .04 + (31.5-lo)*z/FOCAL - .785
    else:
        y = None
    return x, y, z+.31
