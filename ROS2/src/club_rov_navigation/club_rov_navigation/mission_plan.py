"""Bounded launch-relative mission geometry; independent of ROS/MAVLink."""
import math


def wrap(angle):
    return (angle+math.pi) % (2*math.pi)-math.pi


def command(x, y, yaw, depth, target, max_speed=0.15):
    """ROS FLU goal -> BODY_FRD velocity and clockwise yaw rate."""
    tx, ty, td, heading = target
    values = (x, y, yaw, depth, *target, max_speed)
    if not all(math.isfinite(v) for v in values) or max_speed <= 0:
        raise ValueError('Invalid mission state')
    ex, ey = tx-x, ty-y
    vx, vy = .65*ex, .65*ey
    scale = min(1.0, max_speed/max(math.hypot(vx, vy), 1e-9))
    vx, vy = vx*scale, vy*scale
    c, s = math.cos(yaw), math.sin(yaw)
    forward, left = c*vx+s*vy, -s*vx+c*vy
    down = max(-.18, min(.18, .7*(td-depth)))
    clockwise = max(-.30, min(.30, -1.1*wrap(heading-yaw)))
    return forward, -left, down, clockwise


def reached(x, y, yaw, depth, speed, target):
    return (math.hypot(target[0]-x, target[1]-y) < .22 and
            abs(target[2]-depth) < .08 and abs(wrap(target[3]-yaw)) < .15 and speed < .12)


def goals(x, y, yaw, distance, depth):
    if not 1 <= distance <= 6 or not .6 <= depth <= 2.3:
        raise ValueError('Simulation mission limits: distance 1-6 m, depth 0.6-2.3 m')
    far = x+distance*math.cos(yaw), y+distance*math.sin(yaw)
    reverse = wrap(yaw+math.pi)
    return [('DIVE', (x, y, depth, yaw)), ('OUTBOUND', (*far, depth, yaw)),
            ('TURN', (*far, depth, reverse)), ('RETURN', (x, y, depth, reverse)),
            ('SURFACE', (x, y, .10, reverse))]
