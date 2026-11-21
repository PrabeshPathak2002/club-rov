"""Sensor-only learned target-state estimator and feedback policy.

The eight extra recurrent values hold integrated measured odometry, yaw and time.
No course transforms or judge state are read at inference.
"""
import torch
from torch import nn


class GeometryPolicy(nn.Module):
    memory_width = 136

    def __init__(self):
        super().__init__()
        self.visual = nn.Sequential(nn.Conv2d(3, 24, 5, 2, 2), nn.ReLU(),
            nn.Conv2d(24, 48, 3, 2, 1), nn.ReLU(), nn.Conv2d(48, 64, 3, 2, 1), nn.ReLU(),
            nn.Flatten(), nn.Linear(5632, 128), nn.ReLU())
        self.fuse = nn.Sequential(nn.Linear(128 + 16 + 7 + 18, 256), nn.ReLU())
        self.memory = nn.LSTM(256, 64, batch_first=True)
        self.state = nn.Sequential(nn.Linear(64, 128), nn.ReLU(), nn.Linear(128, 5))
        self.phase = nn.Linear(64, 5)
        self.register_buffer('pixel_x', torch.linspace(-1, 1, 84)[None, None, None, :])
        self.register_buffer('pixel_y', torch.linspace(-1, 1, 64)[None, None, :, None])

    def visual_features(self, images):
        r, g, b = images[:, 0:1], images[:, 1:2], images[:, 2:3]
        masks = [(r > .3) & (g > .25) & (r > b * 1.3) & (g > b * 1.3),
                 (g > r * 1.25) & (g > b * 1.12),
                 (r + g + b < .9)]
        rows = []
        for mask in masks:
            m = mask.float()
            count = m.sum((2, 3)).clamp_min(1)
            x = (m * self.pixel_x).sum((2, 3)) / count
            y = (m * self.pixel_y).sum((2, 3)) / count
            xx = (m * self.pixel_x.square()).sum((2, 3)) / count
            yy = (m * self.pixel_y.square()).sum((2, 3)) / count
            rows.extend([count / (64 * 84), x, y, xx, yy, (count > 5).float()])
        return torch.cat(rows, 1)

    def encode(self, images, sensors, recurrent):
        # images B,T,C,H,W; measurements are body FLU, Unity command axes X/Y/Z.
        batch, length = sensors.shape[:2]
        previous = recurrent[:, 0, 128:]
        yaw = previous[:, 2:3] + torch.cumsum(-sensors[:, :, 5] * .2 * sensors[:, :, 15], 1)
        right = -sensors[:, :, 8] * 2 * sensors[:, :, 11]
        forward = sensors[:, :, 7] * 2 * sensors[:, :, 11]
        vx = torch.cos(yaw) * right + torch.sin(yaw) * forward
        vz = -torch.sin(yaw) * right + torch.cos(yaw) * forward
        x = previous[:, 0:1] + torch.cumsum(vx * .1, 1)
        z = previous[:, 1:2] + torch.cumsum(vz * .1, 1)
        clock = previous[:, 3:4] + torch.cumsum(torch.ones_like(yaw) * .1, 1)
        odometry = torch.stack([x / 12, z / 12, torch.sin(yaw), torch.cos(yaw),
                                clock / 150, vx / 2, vz / 2], 2)
        frames = images.reshape(-1, 3, 64, 84)
        visual = self.visual(frames).reshape(batch, length, 128)
        features = self.visual_features(frames).reshape(batch, length, 18)
        encoded = self.fuse(torch.cat([visual, sensors, odometry, features], 2))
        hidden = recurrent[:, 0, :64].unsqueeze(0).contiguous()
        cell = recurrent[:, 0, 64:128].unsqueeze(0).contiguous()
        output, (h, c) = self.memory(encoded, (hidden, cell))
        tail = torch.stack([x[:, -1], z[:, -1], yaw[:, -1], clock[:, -1],
                            vx[:, -1], vz[:, -1], x[:, -1] * 0, z[:, -1] * 0], 1)
        memory = torch.cat([h[0], c[0], tail], 1).unsqueeze(1)
        return output, memory, torch.stack([x, -sensors[:, :, 6] * 4, z, yaw], 2)

    def estimate(self, images, sensors, recurrent, goal_phases=None):
        output, memory, _ = self.encode(images, sensors, recurrent)
        state = self.state(output if goal_phases is None else torch.cat([output, goal_phases], 2))
        state = torch.cat([state[:, :, :3] * 3, state[:, :, 3:]], 2)
        return state, self.phase(output), memory

    @staticmethod
    def control(state, phase, sensors):
        mode = torch.argmax(phase, dim=1)
        offset = state[:, :3]
        angle = torch.atan2(state[:, 3], state[:, 4])
        gate = (mode < 3).float()
        dock = (mode == 4).float()
        x = offset[:, 0] + gate * torch.sin(angle) * .7
        z = offset[:, 2] + gate * torch.cos(angle) * .7
        above = dock * ((offset[:, 0].square() + offset[:, 2].square()) > .04).float()
        y = offset[:, 1] + above * .5
        velocity = torch.stack([-sensors[:, 8] * 2, sensors[:, 9] * 2, sensors[:, 7] * 2], 1)
        command = (.55 * torch.stack([x, y, z], 1) - velocity).clamp(-.6, .6)
        yaw = (.8 * angle + .8 * sensors[:, 5] * 2).clamp(-.5, .5)
        return torch.cat([command, yaw[:, None]], 1)

    def forward(self, obs_0, obs_1, recurrent_in):
        state, phase, memory = self.estimate(obs_0[:, None], obs_1[:, None], recurrent_in)
        return self.control(state[:, 0], phase[:, 0], obs_1), memory, state[:, 0], phase[:, 0]


class GoalGeometryPolicy(GeometryPolicy):
    """Goal query comes from the runner's sensor-derived task tracker, not judge state."""
    def __init__(self):
        super().__init__()
        self.state = nn.Sequential(nn.Linear(69, 128), nn.ReLU(), nn.Linear(128, 5))

    def forward(self, obs_0, obs_1, recurrent_in, goal_phase):
        state, phase, memory = self.estimate(obs_0[:, None], obs_1[:, None], recurrent_in, goal_phase[:, None])
        return self.control(state[:, 0], goal_phase, obs_1), memory, state[:, 0], phase[:, 0]


class WorldGeometryPolicy(GoalGeometryPolicy):
    """Estimate every landmark, with measured odometry handled analytically."""
    def __init__(self):
        super().__init__()
        self.state = nn.Sequential(nn.Linear(64, 128), nn.ReLU(), nn.Linear(128, 25))

    def estimate_all(self, images, sensors, recurrent):
        output, memory, pose = self.encode(images, sensors, recurrent)
        mapped = self.state(output).reshape(*output.shape[:2], 5, 5)
        delta = mapped[:, :, :, :3] * 3 - pose[:, :, None, :3]
        sine, cosine = torch.sin(pose[:, :, 3:4]), torch.cos(pose[:, :, 3:4])
        x = cosine * delta[:, :, :, 0] - sine * delta[:, :, :, 2]
        z = sine * delta[:, :, :, 0] + cosine * delta[:, :, :, 2]
        heading_sine = mapped[:, :, :, 3] * cosine - mapped[:, :, :, 4] * sine
        heading_cosine = mapped[:, :, :, 4] * cosine + mapped[:, :, :, 3] * sine
        state = torch.stack([x, delta[:, :, :, 1], z, heading_sine, heading_cosine], 3)
        return state, self.phase(output), memory

    def estimate(self, images, sensors, recurrent, goal_phases=None):
        state, phase, memory = self.estimate_all(images, sensors, recurrent)
        selected = (state * goal_phases[:, :, :, None]).sum(2)
        return selected, phase, memory
