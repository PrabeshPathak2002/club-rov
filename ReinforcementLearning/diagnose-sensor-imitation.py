"""Replay expert sensor observations through a student (not closed-loop evaluation).

Reports executed-command errors by course phase, with recurrent state reset only
at episode boundaries. Rewards classify report rows; they never enter inference.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
import onnxruntime as ort
from mlagents.trainers.demo_loader import load_demonstration
from mlagents_envs.rpc_utils import steps_from_proto


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--policy', required=True)
    parser.add_argument('--demos', required=True)
    parser.add_argument('--episodes', type=int, default=12)
    parser.add_argument('--output', required=True)
    parser.add_argument('--reset-at-hold-approaches', action='store_true',
                        help='Diagnostic ablation: discard memory at the hover and dock transitions')
    args = parser.parse_args()
    manifest = json.loads((Path(args.demos) / 'alignment.json').read_text())
    if manifest.get('output_action_semantics') != 'next_action' or manifest.get('vector_observation_size') != 16:
        parser.error('Expected aligned sensor16 demonstrations')
    if args.episodes < 1:
        parser.error('episodes must be positive')
    settings = ort.SessionOptions()
    settings.intra_op_num_threads = settings.inter_op_num_threads = 1
    model = ort.InferenceSession(args.policy, settings, providers=['CPUExecutionProvider'])
    groups = {key: [] for key in ['gates', 'hover_approach', 'hover_hold', 'dock_approach', 'dock_hold']}
    episodes = 0
    for path in sorted(Path(args.demos).glob('*.demo')):
        spec, pairs, _ = load_demonstration(str(path))
        if [tuple(obs.shape) for obs in spec.observation_specs] != [(3, 64, 84), (16,)]:
            raise ValueError('Unexpected expert observation contract')
        episode = []
        for pair in pairs:
            episode.append(pair)
            if not pair.agent_info.done:
                continue
            events = [i for i, item in enumerate(episode) if item.agent_info.reward >= 1]
            if len(events) == 5 and episode[-1].agent_info.reward >= 5:
                memory = np.zeros((1, 1, 128), np.float32)
                for i, item in enumerate(episode[:-1]):
                    if args.reset_at_hold_approaches and i in (events[2], events[3]):
                        memory.fill(0)
                    decision, _ = steps_from_proto([item.agent_info], spec)
                    action, memory = model.run(['deterministic_continuous_actions', 'recurrent_out'],
                        {'obs_0': decision.obs[0], 'obs_1': decision.obs[1], 'recurrent_in': memory})
                    key = ('gates' if i < events[2] else
                           'hover_approach' if i < events[3] - 30 else
                           'hover_hold' if i < events[3] else
                           'dock_approach' if i < len(episode) - 30 else 'dock_hold')
                    expert = np.asarray(item.action_info.continuous_actions)
                    groups[key].append((action[0].copy(), expert.copy()))
                episodes += 1
            episode = []
            if episodes >= args.episodes:
                break
        if episodes >= args.episodes:
            break
    report = dict(mode='expert_observation_replay_not_closed_loop', episodes=episodes,
                  policy_sha256=hashlib.sha256(Path(args.policy).read_bytes()).hexdigest(),
                  memory_reset_at_hold_approaches=args.reset_at_hold_approaches,
                  command_order=['starboard', 'up', 'forward', 'yaw'], phases={})
    for key, rows in groups.items():
        if not rows:
            continue
        student, expert = np.asarray(rows).transpose(1, 0, 2)
        report['phases'][key] = dict(frames=len(rows),
            mean_absolute_error=np.abs(student - expert).mean(axis=0).tolist(),
            student_mean_absolute_command=np.abs(student).mean(axis=0).tolist(),
            expert_mean_absolute_command=np.abs(expert).mean(axis=0).tolist())
    Path(args.output).write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2), flush=True)


if __name__ == '__main__':
    main()
