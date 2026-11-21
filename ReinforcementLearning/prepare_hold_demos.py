"""Retain full-course demonstrations and upsample their final stationary holds.

Selection uses training reward events only. No judge information is added to
observations, and already-aligned actions are never shifted again.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import shutil

from google.protobuf.internal.decoder import _DecodeVarint32
from mlagents.trainers.demo_loader import INITIAL_POS, load_demonstration, write_demo
from mlagents_envs.communicator_objects.brain_parameters_pb2 import BrainParametersProto
from mlagents_envs.communicator_objects.demonstration_meta_pb2 import DemonstrationMetaProto


def hold_clips(pairs, window=30):
    clips, episode = [], []
    for pair in pairs:
        episode.append(pair)
        if not pair.agent_info.done:
            continue
        events = [index for index, item in enumerate(episode) if item.agent_info.reward >= 1]
        if len(events) == 5 and episode[-1].agent_info.reward >= 5:
            # Hover's bonus observation already belongs to the next (dock)
            # objective. Stop immediately before it to avoid teaching that turn.
            for kind, end in [('hover', events[3]), ('dock', len(episode))]:
                start = end - window
                if start < 0:
                    continue
                clip = copy.deepcopy(episode[start:end])
                clip[-1].agent_info.done = True
                clip[-1].action_info.continuous_actions[:] = [0.] * 4
                clips.append((kind, clip))
        episode = []
    return clips


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--input', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--copies', type=int, default=3)
    args = parser.parse_args()
    source, out = Path(args.input), Path(args.output)
    manifest = json.loads((source / 'alignment.json').read_text())
    if manifest.get('dataset_kind') == 'full_courses_with_hold_clips':
        parser.error('Input is already hold-focused')
    if manifest.get('schema') != 2 or manifest.get('vector_observation_size') != 16 or manifest.get('output_action_semantics') != 'next_action':
        parser.error('Expected aligned schema2 sensor16 recordings')
    if args.copies not in range(1, 9):
        parser.error('copies must be 1..8')
    out.mkdir(parents=True, exist_ok=False)
    groups = {'hover': [], 'dock': []}
    prototype = None
    for path in sorted(source.glob('*.demo')):
        shutil.copy2(path, out / path.name)
        spec, pairs, _ = load_demonstration(str(path))
        if [tuple(obs.shape) for obs in spec.observation_specs] != [(3, 64, 84), (16,)]:
            raise ValueError('Wrong observation contract')
        if prototype is None:
            raw = path.read_bytes()
            length, pos = _DecodeVarint32(raw, 0)
            metadata = DemonstrationMetaProto.FromString(raw[pos:pos + length])
            length, pos = _DecodeVarint32(raw, INITIAL_POS)
            brain = BrainParametersProto.FromString(raw[pos:pos + length])
            prototype = metadata, brain
        for kind, clip in hold_clips(pairs):
            groups[kind].extend(clip)
    manifest['dataset_kind'] = 'full_courses_with_hold_clips'
    manifest['hold_clips'] = {}
    for kind, pairs in groups.items():
        if not pairs:
            raise ValueError('No unambiguous ' + kind + ' holds found')
        metadata, brain = copy.deepcopy(prototype)
        metadata.number_steps = len(pairs)
        metadata.number_episodes = sum(pair.agent_info.done for pair in pairs)
        metadata.mean_reward = sum(pair.agent_info.reward for pair in pairs) / metadata.number_episodes
        for index in range(args.copies):
            path = out / f'hold-{kind}-{index}.demo'
            write_demo(str(path), metadata, brain, pairs)
            manifest['files'].append(dict(file=path.name, frames=len(pairs), episodes=metadata.number_episodes,
                                         kind=kind, sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        manifest['hold_clips'][kind] = dict(episodes=metadata.number_episodes, frames=len(pairs), copies=args.copies)
    (out / 'alignment.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print(json.dumps(manifest['hold_clips']), flush=True)


if __name__ == '__main__':
    main()
