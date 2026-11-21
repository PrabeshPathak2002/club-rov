"""Audit rendered sensor-policy inputs with zero actions before starting training."""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import shlex
import subprocess

import numpy as np
from PIL import Image
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.side_channel.environment_parameters_channel import EnvironmentParametersChannel
from mlagents_envs.side_channel.stats_side_channel import StatsSideChannel


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--out')
    parser.add_argument('--practice', choices=['hover', 'dock'])
    parser.add_argument('--approach-distance', type=float, default=0)
    options = parser.parse_args()
    if not 0 <= options.approach_distance <= 4 or (options.approach_distance and not options.practice):
        parser.error('approach-distance requires practice and must be 0..4 metres')
    out = Path(options.out or Path.home() / 'club-rov-rl-results' /
               ('sensor-preflight-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')))
    out.mkdir(parents=True, exist_ok=False)
    player = Path(__file__).resolve().parents[1] / 'Simulation/ClubROVSim/Builds/SensorTraining/ClubROVSim.exe'
    wrapper = out / 'SensorPreflight.x86_64'
    wrapper.write_text('#!/usr/bin/env bash\nargs=()\nwhile (($#)); do\n'
                       'if [[ $1 == -logFile && $# -ge 2 ]]; then args+=(-logFile "$(wslpath -w "$2")"); shift 2\n'
                       'else args+=("$1"); shift; fi\ndone\n'
                       f'exec {shlex.quote(str(player))} "${{args[@]}}"\n')
    wrapper.chmod(0o700)
    engine, stats = EngineConfigurationChannel(), StatsSideChannel()
    parameters = EnvironmentParametersChannel()
    if options.practice:
        parameters.set_float_parameter('practice_approach_distance', options.approach_distance)
        parameters.set_float_parameter('practice_hover_probability', 1 if options.practice == 'hover' else 0)
        parameters.set_float_parameter('practice_dock_probability', 1 if options.practice == 'dock' else 0)
        for key in ['practice_position_radius', 'practice_depth_radius', 'practice_heading_range']:
            parameters.set_float_parameter(key, 0)
    engine.set_configuration_parameters(time_scale=10, target_frame_rate=-1)
    env = UnityEnvironment(file_name=str(wrapper.with_suffix('')), base_port=6600, seed=9910,
                           no_graphics=False, side_channels=[engine, stats, parameters], timeout_wait=300,
                           additional_args=['-batchmode', '-job-worker-count', '1', '--rov-rl-sensors'],
                           log_folder=str(out))
    images, imu_valid, dvl_valid, teachers = 0, 0, 0, []
    practice_success, full_samples, visible_resets = [], [], []
    first_image = last_image = None
    previous_actions = np.zeros(4, np.float32)
    depths = []
    try:
        env.reset()
        if len(env.behavior_specs) != 1:
            raise RuntimeError('Expected exactly one sensor-policy behavior')
        name, spec = next(iter(env.behavior_specs.items()))
        shapes = [tuple(s.shape) for s in spec.observation_specs]
        if not name.startswith('PoolCourseSensors?') or sorted(shapes, key=len) != [(16,), (3, 64, 84)]:
            raise RuntimeError(f'Wrong actor observation contract: {name}, {shapes}')
        for step in range(64):
            decision, terminal = env.get_steps(name)
            if len(decision):
                for obs in decision.obs:
                    if not np.isfinite(obs).all():
                        raise RuntimeError('Nonfinite policy input')
                    if obs.ndim == 4:
                        image = obs[0].transpose(1, 2, 0)
                        if image.max() > .05 and image.var() > .0001:
                            if images == 0:
                                Image.fromarray((image.clip(0, 1) * 255).astype(np.uint8)).save(out / 'actor-camera.png')
                                first_image = image.copy()
                            last_image = image.copy()
                            if step == 8:
                                Image.fromarray((image.clip(0, 1) * 255).astype(np.uint8)).save(out / 'actor-camera-settled.png')
                            images += 1
                    else:
                        row = obs[0]
                        if np.abs(row).max() > 1.001:
                            raise RuntimeError('Invalid sensor normalization')
                        imu_valid += int(row[15] == 1)
                        dvl_valid += int(row[11] == 1)
                        if row[15] == 1:
                            depths.append(float(row[6] * 4))
                previous_actions = np.array([0, 0, 0, .1 if step >= 32 else 0], np.float32)
                env.set_actions(name, ActionTuple(continuous=np.tile(previous_actions, (len(decision), 1))))
            samples = stats.get_and_reset_stats()
            if options.practice:
                visible_resets.extend(value for value, _ in samples.get('Practice/TargetVisibleAtReset', []))
                practice_success.extend(value for value, _ in samples.get(
                    'Practice/HoverCompleted' if options.practice == 'hover' else 'Practice/DockCompleted', []))
                full_samples.extend(samples.get('Course/Completed', []))
            for value, _ in samples.get('Sensor/TeacherActive', []):
                teachers.append(value)
            env.step()
        if not images or not imu_valid or not dvl_valid or not teachers or any(teachers):
            raise RuntimeError(f'Input audit failed: images={images}, imu={imu_valid}, dvl={dvl_valid}, teacher={teachers}')
        if options.practice and (full_samples or (not options.approach_distance and not any(practice_success))):
            raise RuntimeError(f'Practice reset/judge/stats failed: success={practice_success}, full_samples={full_samples}')
        if options.approach_distance and not any(visible_resets):
            raise RuntimeError('Visible-target practice reset was not reported')
        image_change = float(np.abs(last_image - first_image).mean())
        if image_change < .0001:
            raise RuntimeError('Camera image did not update while the vehicle turned')
        Image.fromarray((last_image.clip(0, 1) * 255).astype(np.uint8)).save(out / 'actor-camera-later.png')
        report = dict(passed=True, behavior=name, observation_shapes=shapes, rendered_frames=images,
                      practice_mode=options.practice, practice_successes=sum(practice_success),
                      approach_distance_m=options.approach_distance, visible_resets=len(visible_resets),
                      imu_valid=imu_valid, dvl_valid=dvl_valid, teacher_active=False,
                      image_mean_absolute_change=image_change,
                      pressure_depth_range_m=[min(depths), max(depths)])
        (out / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
        print(json.dumps(report), flush=True)
        print('Audit artifacts:', out, flush=True)
    finally:
        env.close()


if __name__ == '__main__':
    main()
