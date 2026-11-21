"""Tracker 650-style VISION_POSITION_DELTA integration (body FRD frame)."""

MAX_GAP = 0.5


def frd(flu):
    return (flu[0], -flu[1], -flu[2])


class DeltaIntegrator:
    """Turns sensor-frame DVL velocity and body gyro rates into position and angle deltas."""

    def __init__(self):
        self.angle = [0.0, 0.0, 0.0]
        self.imu_stamp = None
        self.dvl_stamp = None

    def add_gyro(self, stamp, rates_flu):
        if self.imu_stamp is not None and 0 < stamp-self.imu_stamp <= MAX_GAP:
            dt = stamp-self.imu_stamp
            for i, rate in enumerate(frd(rates_flu)):
                self.angle[i] += rate*dt
        self.imu_stamp = stamp

    def add_velocity(self, stamp, velocity_flu, valid):
        """Returns (dt, angle_delta, position_delta), or None when nothing should be sent."""
        previous, self.dvl_stamp = self.dvl_stamp, stamp if valid else None
        angle, self.angle = self.angle, [0.0, 0.0, 0.0]
        if not valid or previous is None or not 0 < stamp-previous <= MAX_GAP:
            return None
        dt = stamp-previous
        return dt, angle, [v*dt for v in frd(velocity_flu)]
