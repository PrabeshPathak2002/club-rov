"""Launch-relative ROS FLU to ArduPilot LOCAL_FRD/BODY_FRD (metres, radians)."""
import math


def multiply(a, b):
    w, x, y, z = a
    v, i, j, k = b
    return (w*v-x*i-y*j-z*k, w*i+x*v+y*k-z*j,
            w*j-x*k+y*v+z*i, w*k+x*j-y*i+z*v)


def convert(position, quaternion_xyzw, velocity, rates, heading):
    values = (*position, *quaternion_xyzw, *velocity, *rates, heading)
    if not all(math.isfinite(v) for v in values):
        raise ValueError('Nonfinite external navigation')
    x, y, z, w = quaternion_xyzw
    if abs(x*x+y*y+z*z+w*w-1) > 0.01:
        raise ValueError('External navigation quaternion is not normalized')
    c, s = math.cos(heading), math.sin(heading)
    # Flip left/up to right/down, then rotate launch forward onto captured heading.
    p = (c*position[0]+s*position[1], s*position[0]-c*position[1], -position[2])
    q = multiply((math.cos(heading/2), 0, 0, math.sin(heading/2)), (w, x, -y, -z))
    return p, q, (velocity[0], -velocity[1], -velocity[2]), (rates[0], -rates[1], -rates[2])


def pose_covariance(covariance):
    """Conservative diagonal covariance: orientation/position axes after rotation.

    The pinned firmware consumes sums of position/angle diagonal variances only.
    Isotropic maxima preserve those bounds without pretending correlations survive.
    """
    if len(covariance) != 36 or not all(math.isfinite(v) for v in covariance):
        raise ValueError('Invalid pose covariance')
    diagonal = [covariance[i*7] for i in range(6)]
    if any(v < 0 for v in diagonal) or max(diagonal[:3]) > 4 or max(diagonal[3:]) > 1:
        raise ValueError('External navigation uncertainty exceeds simulation limits')
    result = [0.0]*21
    for i in (0, 6, 11):
        result[i] = max(0.01, *diagonal[:3])
    for i in (15, 18, 20):
        result[i] = max(0.0025, *diagonal[3:])
    return result
