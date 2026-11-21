"""Evaluate a fixed ONNX policy on seeded fixed or randomised course episodes."""
import argparse
import hashlib
import json
import multiprocessing as mp
import os
from pathlib import Path
import shlex
import tempfile
import time


def completion_metric(practice):
    return {'full': 'Course/Completed', 'hover': 'Practice/HoverCompleted',
            'dock': 'Practice/DockCompleted'}[practice]


def evaluate_worker(options, worker, episodes):
    import numpy as np
    import onnxruntime as ort
    from mlagents_envs.base_env import ActionTuple
    from mlagents_envs.environment import UnityEnvironment
    from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
    from mlagents_envs.side_channel.environment_parameters_channel import EnvironmentParametersChannel
    from mlagents_envs.side_channel.stats_side_channel import StatsSideChannel

    session_options = ort.SessionOptions()
    session_options.intra_op_num_threads = 1
    session_options.inter_op_num_threads = 1
    policy = ort.InferenceSession(options.policy, session_options, providers=['CPUExecutionProvider'])
    input_name = policy.get_inputs()[0].name
    sensor_inputs = [item for item in policy.get_inputs() if item.name.startswith('obs_')]
    memory_input = next((item for item in policy.get_inputs() if item.name == 'recurrent_in'), None)
    if options.sensors and ([item.name for item in sensor_inputs] != ['obs_0', 'obs_1'] or
                           memory_input is None or memory_input.shape[1] != 1 or memory_input.shape[2] not in (128, 136)):
        raise ValueError('Expected two sensor inputs and supported recurrent memory [batch,1,128|136]')
    memories = {}
    navigator = None
    if options.navigation:
        from tracked_geometry import TrackedGeometry
        navigator = TrackedGeometry(options.policy)
    teacher_samples = 0
    capture = Path(options.capture) if options.capture and worker == 0 else None
    trace = []
    if capture:
        capture.mkdir(parents=True, exist_ok=False)
    action_name = 'deterministic_continuous_actions'
    if action_name not in [output.name for output in policy.get_outputs()]:
        raise ValueError('Policy must export deterministic_continuous_actions')
    parameters = EnvironmentParametersChannel()
    parameters.set_float_parameter('course_randomisation', 1.0 if options.profile == 'randomised' else 0.0)
    parameters.set_float_parameter('practice_hover_probability', 1 if options.practice == 'hover' else 0)
    parameters.set_float_parameter('practice_dock_probability', 1 if options.practice == 'dock' else 0)
    if options.approach_distance > 0:
        parameters.set_float_parameter('practice_approach_distance', options.approach_distance)
        parameters.set_float_parameter('practice_position_radius', .3)
        parameters.set_float_parameter('practice_depth_radius', .1)
        parameters.set_float_parameter('practice_heading_range', 10)
    engine = EngineConfigurationChannel()
    engine.set_configuration_parameters(time_scale=20, target_frame_rate=-1)
    stats = StatsSideChannel()
    started = time.monotonic()
    with tempfile.TemporaryDirectory(prefix='club-rov-evaluation-') as directory:
        wrapper = Path(directory) / 'ClubROVSim.x86_64'
        wrapper.write_text('#!/usr/bin/env bash\n'
                           'args=()\nwhile (($#)); do\n'
                           '  if [[ $1 == -logFile && $# -ge 2 ]]; then args+=(-logFile "$(wslpath -w "$2")"); shift 2\n'
                           '  else args+=("$1"); shift; fi\ndone\n'
                           f'exec {shlex.quote(str(Path(options.player).resolve()))} "${{args[@]}}"\n')
        wrapper.chmod(0o700)
        environment = UnityEnvironment(file_name=str(wrapper.with_suffix('')), worker_id=worker,
                                       base_port=options.base_port, seed=options.seed + worker,
                                       no_graphics=not options.sensors, side_channels=[parameters, engine, stats],
                                       additional_args=['-batchmode', '-job-worker-count', '1'] +
                                           (['--rov-rl-sensors'] if options.sensors else []),
                                       log_folder=directory, timeout_wait=120)
        completion = []
        objectives = []
        try:
            environment.reset()
            behavior = next(iter(environment.behavior_specs))
            if options.sensors:
                shapes = [tuple(item.shape) for item in environment.behavior_specs[behavior].observation_specs]
                if not behavior.startswith('PoolCourseSensors?') or shapes != [(3, 64, 84), (16,)]:
                    raise ValueError(f'Invalid sensor actor contract: {behavior}, {shapes}')
            while len(completion) < episodes:
                decisions, terminals = environment.get_steps(behavior)
                values = stats.get_and_reset_stats()
                if options.sensors:
                    teacher_values = values.get('Sensor/TeacherActive', [])
                    teacher_samples += len(teacher_values)
                    if any(value != 0 for value, _ in teacher_values):
                        raise RuntimeError('Teacher active during student evaluation')
                    for agent in terminals.agent_id:
                        memories.pop(int(agent), None)
                        if navigator is not None:
                            navigator.reset()
                completion.extend(value for value, _ in values.get(completion_metric(options.practice), []))
                objectives.extend(value for value, _ in values.get('Course/Objectives', []))
                if any(value not in (0.0, 1.0) for value in completion[-1:]):
                    raise ValueError('Expected a binary completion sample per episode')
                if len(completion) >= episodes:
                    break
                if len(decisions):
                    if options.sensors:
                        if not all(np.isfinite(obs).all() for obs in decisions.obs):
                            raise ValueError('Nonfinite sensor observations')
                        feed = {item.name: decisions.obs[int(item.name[4:])] for item in sensor_inputs}
                        outputs = [action_name]
                        if memory_input is not None:
                            width = int(memory_input.shape[-1])
                            feed['recurrent_in'] = np.stack([memories.get(int(agent), np.zeros((1, width), np.float32))
                                                            for agent in decisions.agent_id], axis=0)
                            outputs.append('recurrent_out')
                        if capture and navigator is None and 'state_estimate' in [item.name for item in policy.get_outputs()]:
                            outputs += ['state_estimate', 'phase_logits']
                        if navigator is not None:
                            if len(decisions) != 1:
                                raise RuntimeError('Navigation runner expects one vehicle per environment')
                            result = [navigator.predict(decisions.obs[0][0], decisions.obs[1][0])[None], navigator.memory]
                        else:
                            result = policy.run(outputs, feed)
                        actions = result[0]
                        if memory_input is not None:
                            for index, agent in enumerate(decisions.agent_id):
                                memories[int(agent)] = result[1][index, :, :].copy()
                    else:
                        actions = policy.run([action_name], {input_name: decisions.obs[0]})[0]
                    if not np.isfinite(actions).all():
                        raise ValueError('Policy produced nonfinite actions')
                    actions = np.clip(actions * options.action_gain, -1, 1)
                    if capture and len(completion) >= options.capture_episode and len(trace) < options.capture_limit:
                        if not options.sensors:
                            raise ValueError('Sensor capture requires --sensors')
                        trace.append(dict(step=len(trace), agent=int(decisions.agent_id[0]),
                                          sensors=decisions.obs[1][0].tolist(), actions=actions[0].tolist()))
                        if options.sensors and 'state_estimate' in outputs:
                            trace[-1]['state_estimate'] = result[outputs.index('state_estimate')][0].tolist()
                            trace[-1]['phase_logits'] = result[outputs.index('phase_logits')][0].tolist()
                        if navigator is not None:
                            trace[-1]['navigation'] = navigator.debug.copy()
                        if (len(trace) - 1) % 20 == 0:
                            from PIL import Image
                            pixels = (decisions.obs[0][0].transpose(1, 2, 0).clip(0, 1) * 255).astype(np.uint8)
                            Image.fromarray(pixels).save(capture / f'{len(trace)-1:04d}.png')
                    environment.set_actions(behavior, ActionTuple(continuous=actions.astype(np.float32)))
                environment.step()
                if time.monotonic() - started > options.timeout:
                    raise TimeoutError(f'Worker {worker} evaluated only {len(completion)}/{episodes} episodes')
        finally:
            environment.close()
            if capture:
                (capture / 'actor-trace.json').write_text(json.dumps(trace) + '\n')
    if options.require_objectives and len(objectives) != len(completion):
        raise RuntimeError('Every completed episode must emit Course/Objectives; rebuild the player')
    if options.sensors and teacher_samples == 0:
        raise RuntimeError('No teacher-absence statistics received; rebuild the sensor player')
    return dict(worker=worker, seed=options.seed + worker, completed=completion[:episodes],
                objectives=objectives[:episodes], seconds=time.monotonic() - started,
                teacher_absence_samples=teacher_samples if options.sensors else None)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--policy', required=True)
    parser.add_argument('--sensors', action='store_true', help='Rendered camera + sensor policy with recurrent memory')
    parser.add_argument('--navigation', action='store_true', help='Use learned geometry and sensor-derived task tracking')
    parser.add_argument('--practice', choices=['full', 'hover', 'dock'], default='full',
                        help='Practice tests are reported separately and never count as full courses')
    parser.add_argument('--approach-distance', type=float, default=0, help='Practice starts facing a visible target from this distance (metres)')
    parser.add_argument('--player')
    parser.add_argument('--profile', choices=['fixed', 'randomised'], required=True)
    parser.add_argument('--episodes', type=int, default=100)
    parser.add_argument('--workers', type=int, default=4)
    parser.add_argument('--seed', type=int, default=7300)
    parser.add_argument('--base-port', type=int, default=6200)
    parser.add_argument('--timeout', type=float, default=1800)
    parser.add_argument('--output', required=True)
    parser.add_argument('--require-objectives', action='store_true')
    parser.add_argument('--capture', help='New directory for first worker sensor frames/actions (first 512 decisions)')
    parser.add_argument('--capture-limit', type=int, default=512)
    parser.add_argument('--capture-episode', type=int, default=0)
    parser.add_argument('--action-gain', type=float, default=1.0, help='Diagnostic action scaling; default unchanged')
    options = parser.parse_args()
    if not 1 <= options.capture_limit <= 4096:
        parser.error('capture-limit must be 1..4096')
    if options.capture_episode < 0:
        parser.error('capture-episode must be nonnegative')
    if options.navigation and (not options.sensors or options.practice != 'full'):
        parser.error('Navigation evaluation requires sensors and a full-course start')
    if not 0 <= options.approach_distance <= 4 or (options.practice == 'full' and options.approach_distance):
        parser.error('approach-distance must be 0..4 and is only for practice tests')
    if options.practice != 'full' and (not options.sensors or options.require_objectives):
        parser.error('Practice tests require --sensors and omit --require-objectives')
    if not 0 < options.action_gain <= 3:
        parser.error('action-gain must be in (0,3]')
    if options.player is None:
        options.player = str(Path(__file__).resolve().parents[1] / 'Simulation/ClubROVSim/Builds' /
                             ('SensorTraining' if options.sensors else 'Training') / 'ClubROVSim.exe')
    if not 1 <= options.workers <= options.episodes or options.episodes < 1:
        parser.error('Use 1 <= workers <= episodes')
    if not Path(options.player).is_file() or not Path(options.policy).is_file():
        parser.error('Player and policy files must exist')
    policy_hash = hashlib.sha256(Path(options.policy).read_bytes()).hexdigest()
    runtime = None
    if options.navigation:
        from tracked_geometry import runtime_fingerprint
        runtime = runtime_fingerprint(options.policy)
    os.environ['OMP_NUM_THREADS'] = '1'
    os.environ['OPENBLAS_NUM_THREADS'] = '1'
    jobs = [(options, worker, options.episodes // options.workers + (worker < options.episodes % options.workers))
            for worker in range(options.workers)]
    with mp.get_context('spawn').Pool(options.workers) as pool:
        results = pool.starmap(evaluate_worker, jobs)
    completed = [value for result in results for value in result['completed']]
    objectives = [value for result in results for value in result['objectives']]
    if hashlib.sha256(Path(options.policy).read_bytes()).hexdigest() != policy_hash:
        raise RuntimeError('Policy changed during evaluation; use an immutable checkpoint')
    if options.navigation and runtime != runtime_fingerprint(options.policy):
        raise RuntimeError('Navigation model/map/runtime changed during evaluation')
    report = dict(policy=str(Path(options.policy).resolve()), profile=options.profile,
                  evaluation_mode='full_course' if options.practice == 'full' else 'practice_' + options.practice,
                  practice_approach_distance_m=options.approach_distance,
                  sensor_policy=options.sensors,
                  action_gain=options.action_gain,
                  policy_sha256=policy_hash,
                  navigation_runtime=runtime,
                  player=str(Path(options.player).resolve()), seed=options.seed, episodes=len(completed),
                  successes=sum(value >= 0.5 for value in completed),
                  completion_rate=sum(completed) / len(completed) if options.practice == 'full' else None,
                  practice_success_rate=sum(completed) / len(completed) if options.practice != 'full' else None,
                  mean_objectives=sum(objectives) / len(objectives) if objectives else None,
                  objectives_histogram={str(stage): objectives.count(stage) for stage in range(6)} if objectives else None,
                  workers=results)
    output = Path(options.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({key: value for key, value in report.items() if key != 'workers'}, indent=2), flush=True)


if __name__ == '__main__':
    main()
