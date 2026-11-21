"""Record expert labels on student-visited approach and recovery states.

Labels arrive through a collection-only statistics channel, never actor inputs.
Each episode starts with a bounded student prefix then expert recovery. Unlike
successful-course recordings, failed rollouts are retained for corrective labels.
"""
import argparse
import copy
import hashlib
import io
import json
from pathlib import Path
import shlex
import tempfile
import time

import numpy as np
import onnxruntime as ort
from PIL import Image
from google.protobuf.internal.decoder import _DecodeVarint32
from mlagents.trainers.demo_loader import INITIAL_POS, load_demonstration, write_demo
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.side_channel.environment_parameters_channel import EnvironmentParametersChannel
from mlagents_envs.side_channel.stats_side_channel import StatsSideChannel
from mlagents_envs.communicator_objects.brain_parameters_pb2 import BrainParametersProto
from mlagents_envs.communicator_objects.demonstration_meta_pb2 import DemonstrationMetaProto


def frame(template, observations, label, reward, done):
    result = copy.deepcopy(template)
    result.agent_info.reward = float(reward)
    result.agent_info.done = bool(done)
    result.agent_info.max_step_reached = False
    pixels = (observations[0].transpose(1, 2, 0).clip(0, 1) * 255).astype(np.uint8)
    stream = io.BytesIO()
    Image.fromarray(pixels).save(stream, format='PNG')
    result.agent_info.observations[0].compressed_data = stream.getvalue()
    result.agent_info.observations[1].float_data.data[:] = observations[1].tolist()
    result.action_info.continuous_actions[:] = label.tolist()
    return result


def teacher_label(samples, terminal_observation=False):
    columns = [samples.get('Correction/Command' + str(i), []) for i in range(4)]
    # EndEpisode also collects a terminal observation. If terminal and reset
    # decision arrive together, their labels are ordered terminal then decision.
    allowed = (1, 2) if terminal_observation else (1,)
    if len({len(c) for c in columns}) != 1 or len(columns[0]) not in allowed:
        raise RuntimeError(f'Unsynchronised expert labels: {[len(c) for c in columns]}')
    label = np.array([c[-1][0] for c in columns], np.float32)
    if not np.isfinite(label).all() or np.abs(label).max() > 1.001:
        raise RuntimeError('Invalid expert command')
    return label


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--policy', required=True)
    parser.add_argument('--prototype', required=True, help='Existing aligned sensor16 .demo for its schema')
    parser.add_argument('--practice', choices=['hover', 'dock', 'full'], required=True)
    parser.add_argument('--episodes', type=int, default=16)
    parser.add_argument('--student-steps', type=int, default=20)
    parser.add_argument('--max-decisions', type=int, default=0, help='Explicitly truncate collection episodes; never scored as completion')
    parser.add_argument('--seed', type=int, default=25000)
    parser.add_argument('--randomisation', type=float, default=0)
    parser.add_argument('--base-port', type=int, default=7100)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    if args.episodes < 1 or not 0 <= args.student_steps <= 1000 or not 0 <= args.randomisation <= 1 or args.max_decisions < 0:
        parser.error('Use positive episodes and 0..100 student steps')
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=False)
    spec, prototype, _ = load_demonstration(args.prototype)
    if [tuple(o.shape) for o in spec.observation_specs] != [(3, 64, 84), (16,)]:
        raise ValueError('Expected sensor16 prototype')
    raw = Path(args.prototype).read_bytes()
    length, pos = _DecodeVarint32(raw, 0)
    metadata = DemonstrationMetaProto.FromString(raw[pos:pos + length])
    length, pos = _DecodeVarint32(raw, INITIAL_POS)
    brain = BrainParametersProto.FromString(raw[pos:pos + length])
    config = ort.SessionOptions()
    config.intra_op_num_threads = config.inter_op_num_threads = 1
    policy = ort.InferenceSession(args.policy, config, providers=['CPUExecutionProvider'])
    parameters, engine, stats = EnvironmentParametersChannel(), EngineConfigurationChannel(), StatsSideChannel()
    for key, value in dict(course_randomisation=args.randomisation, practice_hover_probability=int(args.practice == 'hover'),
                           practice_dock_probability=int(args.practice == 'dock'), practice_approach_distance=2,
                           practice_position_radius=.3, practice_depth_radius=.1, practice_heading_range=10,
                           practice_timeout_seconds=30).items():
        parameters.set_float_parameter(key, value)
    engine.set_configuration_parameters(time_scale=20, target_frame_rate=-1)
    player = Path(__file__).resolve().parents[1] / 'Simulation/ClubROVSim/Builds/SensorTraining/ClubROVSim.exe'
    records, success, student_frames, truncated = [], [], 0, 0
    images, sensors, states, starts, landmarks = [], [], [], [], []
    width = int(next(item for item in policy.get_inputs() if item.name == 'recurrent_in').shape[-1])
    if width not in (128, 136):
        raise ValueError('Unsupported student recurrent memory')
    memory = np.zeros((1, 1, width), np.float32)
    episode_step = 0
    with tempfile.TemporaryDirectory(prefix='rov-corrections-') as folder:
        wrapper = Path(folder) / 'Corrections.x86_64'
        wrapper.write_text('#!/usr/bin/env bash\nargs=()\nwhile (($#)); do\n'
                          'if [[ $1 == -logFile && $# -ge 2 ]]; then args+=(-logFile "$(wslpath -w "$2")"); shift 2\n'
                          'else args+=("$1"); shift; fi\ndone\nexec ' + shlex.quote(str(player)) + ' "${args[@]}"\n')
        wrapper.chmod(0o700)
        env = UnityEnvironment(file_name=str(wrapper.with_suffix('')), base_port=args.base_port,
            seed=args.seed, side_channels=[parameters, engine, stats], timeout_wait=120,
            log_folder=str(out),
            additional_args=['-batchmode', '-job-worker-count', '1', '--rov-sensor-corrections',
                             '--rov-demo-seed', str(args.seed)])
        try:
            started = time.monotonic()
            env.reset()
            name, actual = next(iter(env.behavior_specs.items()))
            if not name.startswith('PoolCourseSensors?') or [tuple(o.shape) for o in actual.observation_specs] != [(3, 64, 84), (16,)]:
                raise RuntimeError('Unexpected student observation contract')
            while len(success) < args.episodes:
                if time.monotonic() - started > 1200:
                    raise TimeoutError('Correction collection exceeded 20 minutes; inspect the player log')
                decisions, terminals = env.get_steps(name)
                samples = stats.get_and_reset_stats()
                if len(terminals):
                    records.append(frame(prototype[0], [o[0] for o in terminals.obs], np.zeros(4), terminals.reward[0], True))
                    metric = {'full': 'Course/Completed', 'hover': 'Practice/HoverCompleted', 'dock': 'Practice/DockCompleted'}[args.practice]
                    values = samples.get(metric, [])
                    if len(values) != 1:
                        raise RuntimeError('Missing practice outcome')
                    success.append(values[0][0])
                    memory.fill(0)
                    episode_step = 0
                    print(f'{args.practice}: {len(success)}/{args.episodes}, recovery success {sum(success):.0f}', flush=True)
                    if len(success) == args.episodes:
                        break
                if len(decisions):
                    if not any(value == 1 for value, _ in samples.get('Sensor/TeacherActive', [])):
                        raise RuntimeError('Collection teacher absent')
                    label = teacher_label(samples, bool(len(terminals)))
                    observations = [o[0] for o in decisions.obs]
                    state_columns = [samples.get('Correction/State' + str(i), []) for i in range(6)]
                    if not all(len(c) == len(samples['Correction/Command0']) for c in state_columns):
                        raise RuntimeError('Missing synchronized geometry supervision labels')
                    images.append((observations[0].clip(0, 1) * 255).astype(np.uint8))
                    sensors.append(observations[1].copy())
                    states.append([c[-1][0] for c in state_columns])
                    landmark_columns = [samples.get('Correction/Landmark' + str(i), []) for i in range(25)]
                    if not all(len(c) == len(state_columns[0]) for c in landmark_columns):
                        raise RuntimeError('Missing synchronized landmark supervision')
                    landmarks.append([c[-1][0] for c in landmark_columns])
                    starts.append(episode_step == 0)
                    records.append(frame(prototype[0], observations, label, decisions.reward[0], False))
                    action, memory = policy.run(['deterministic_continuous_actions', 'recurrent_out'],
                        {'obs_0': decisions.obs[0], 'obs_1': decisions.obs[1], 'recurrent_in': memory})
                    # Vary the prefix across episodes, including immediate expert
                    # approaches and longer student mistakes. Every row keeps the
                    # expert label, regardless of the executed rollout command.
                    prefix = args.student_steps * (1 + len(success) % 3) // 2
                    use_student = episode_step < prefix
                    selected = action[0] if use_student else label
                    student_frames += int(use_student)
                    episode_step += 1
                    if args.max_decisions and episode_step >= args.max_decisions:
                        records[-1].agent_info.done = True
                        records[-1].action_info.continuous_actions[:] = [0.] * 4
                        success.append(0)
                        truncated += 1
                        memory.fill(0)
                        episode_step = 0
                        print(f'{args.practice}: {len(success)}/{args.episodes}, truncated (no scored completion)', flush=True)
                        if len(success) == args.episodes:
                            break
                        env.reset()
                        continue
                    env.set_actions(name, ActionTuple(continuous=np.clip(selected[None], -1, 1).astype(np.float32)))
                env.step()
        finally:
            env.close()
    metadata.number_steps = len(records)
    metadata.number_episodes = len(success)
    metadata.mean_reward = sum(p.agent_info.reward for p in records) / len(success)
    demo = out / ('corrections-' + args.practice + '.demo')
    write_demo(str(demo), metadata, brain, records)
    np.savez_compressed(out / 'geometry-supervision.npz', images=np.asarray(images),
                        sensors=np.asarray(sensors), targets=np.asarray(states, np.float32),
                        starts=np.asarray(starts, bool))
    np.savez_compressed(out / 'landmark-supervision.npz', images=np.asarray(images), sensors=np.asarray(sensors),
                        targets=np.asarray(states, np.float32), starts=np.asarray(starts, bool),
                        landmarks=np.asarray(landmarks, np.float32).reshape(-1, 5, 5))
    manifest = dict(schema=2, vector_observation_size=16, output_action_semantics='next_action',
                    dataset_kind='expert_labels_on_student_states', successful_only=False,
                    collection_teacher='hold_feedback_above_dock_v1',
                    policy_sha256=hashlib.sha256(Path(args.policy).read_bytes()).hexdigest(),
                    practice=args.practice, seed=args.seed, episodes=len(success), recovery_successes=sum(success),
                    randomisation=args.randomisation,
                    truncated_episodes=truncated,
                    student_frames=student_frames, teacher_label_frames=len(records)-len(success),
                    files=[dict(file=demo.name, frames=len(records), episodes=len(success))])
    (out / 'alignment.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print(json.dumps(manifest), flush=True)


if __name__ == '__main__':
    main()
