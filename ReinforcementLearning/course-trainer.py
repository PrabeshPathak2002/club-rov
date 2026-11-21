"""ML-Agents 1.1.0 entry point with a Course/Completed-driven curriculum.

ML-Agents' stock curriculum accepts reward/progress, but does not accept an
arbitrary StatsRecorder metric. The adapter below supplies episode completion
samples to the curriculum's reward-measure input only. PPO keeps its normal
shaped rewards, trajectories, critic and optimiser.
"""
from collections import deque
import math
import os
from importlib.metadata import version

from mlagents.trainers import learn
from mlagents.trainers.environment_parameter_manager import EnvironmentParameterManager
from mlagents.trainers.stats import StatsReporter, StatsWriter
from mlagents.trainers.torch_entities.networks import SimpleActor


class FixedSensorNormalizationActor(SimpleActor):
    """Keep the demonstration sensor calibration stable during PPO fine-tuning.

    The critic retains normal running statistics. Actor weights still learn;
    only its input mean/variance remain those saved by the imitation warm-start.
    """
    def update_normalization(self, buffer):
        pass


class CompletionWindow(StatsWriter):
    def __init__(self, behavior='PoolCourse'):
        self.behavior = behavior
        self.values = deque(maxlen=200)

    def on_add_stat(self, category, key, value, aggregation=None):
        if category == self.behavior and key == 'Sensor/TeacherActive' and value != 0:
            raise RuntimeError('Privileged teacher must not actuate during sensor-policy training/evaluation')
        if category == self.behavior and key == 'Course/Completed':
            if not math.isfinite(value) or value not in (0.0, 1.0):
                raise ValueError('Course/Completed must be one binary sample per finished episode')
            self.values.append(value)

    def write_stats(self, category, values, step):
        pass


completion_window = CompletionWindow(os.environ.get('CLUB_ROV_RL_BEHAVIOR', 'PoolCourse'))


class CompletionCurriculum(EnvironmentParameterManager):
    def update_lessons(self, trainer_steps, trainer_max_steps, trainer_reward_buffer):
        curriculum_measure = dict(trainer_reward_buffer)
        curriculum_measure[completion_window.behavior] = list(completion_window.values)
        updated, reset = super().update_lessons(trainer_steps, trainer_max_steps, curriculum_measure)
        if updated:
            completion_window.values.clear()
        return updated, reset


def main():
    if version('mlagents') != '1.1.0':
        raise RuntimeError('The completion curriculum adapter is validated with mlagents 1.1.0')
    # ML-Agents overrides OMP_NUM_THREADS with its own four-thread default.
    # The small 128-unit policy needs one thread; leave CPU headroom for players.
    from mlagents.torch_utils import torch
    torch.set_num_threads(1)
    torch.set_num_interop_threads(1)
    StatsReporter.add_writer(completion_window)
    learn.EnvironmentParameterManager = CompletionCurriculum
    if completion_window.behavior == 'PoolCourseSensors':
        from mlagents.trainers.ppo import trainer as ppo
        from mlagents.trainers.optimizer import torch_optimizer
        from sensor_bc import SensorBCModule
        ppo.SimpleActor = FixedSensorNormalizationActor
        torch_optimizer.BCModule = SensorBCModule
        print('Sensor actor normalization fixed; critic normalization remains adaptive.', flush=True)
    learn.main()


if __name__ == '__main__':
    main()
