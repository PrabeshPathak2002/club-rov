"""Convert ML-Agents 4.2 previous-action recordings to 1.1 next-action labels.

Raw files remain unchanged. Older command-history fields are removed; supervised
action labels move back one observation within each episode.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path

import numpy as np
from google.protobuf.internal.decoder import _DecodeVarint32
from mlagents.trainers.demo_loader import INITIAL_POS, load_demonstration, write_demo
from mlagents_envs.communicator_objects.brain_parameters_pb2 import BrainParametersProto
from mlagents_envs.communicator_objects.demonstration_meta_pb2 import DemonstrationMetaProto


def align_pairs(pairs, include_failures=True):
    result = copy.deepcopy(pairs)
    last_terminal = max((i for i, pair in enumerate(pairs) if pair.agent_info.done), default=-1)
    if last_terminal < 1:
        raise ValueError('No complete episode in recording')
    result = result[:last_terminal + 1]
    for index, pair in enumerate(result):
        if pair.agent_info.done:
            # A terminal observation has no next action. Never copy the next episode.
            pair.action_info.continuous_actions[:] = [0.0] * 4
        else:
            next_pair = pairs[index + 1]
            if next_pair.agent_info.id != pair.agent_info.id:
                raise ValueError('Agent changed without terminal boundary')
            pair.action_info.CopyFrom(next_pair.action_info)
    if not include_failures:
        successful, episode = [], []
        for pair in result:
            episode.append(pair)
            if pair.agent_info.done:
                # RovSensorCourseAgent grants +5 only for course completion
                # (plus the final +2 objective); failures receive -1.
                if pair.agent_info.reward >= 5:
                    successful.extend(episode)
                episode = []
        result = successful
        if not result:
            raise ValueError('No successful full-course demonstrations')
    return result


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--input', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--include-failures', action='store_true', help='Retain failed expert episodes (excluded by default)')
    args = parser.parse_args()
    source, out = Path(args.input), Path(args.output)
    if (source / 'alignment.json').exists():
        parser.error('Input is already aligned')
    files = sorted(source.glob('*.demo'))
    if not files:
        parser.error('No raw recordings')
    out.mkdir(parents=True, exist_ok=False)
    raw_marker = source / 'raw-recording.json'
    marked_previous = raw_marker.is_file() and json.loads(raw_marker.read_text()).get('action_semantics') == 'previous_action'
    report = dict(schema=2, vector_observation_size=16, source_action_semantics='previous_action',
                  output_action_semantics='next_action', successful_only=not args.include_failures, files=[])
    for path in files:
        spec, pairs, _ = load_demonstration(str(path))
        shapes = [tuple(item.shape) for item in spec.observation_specs]
        if shapes not in ([(3, 64, 84), (20,)], [(3, 64, 84), (16,)]):
            raise ValueError('Wrong sensor observation contract')
        rows = [pair for pair in pairs if not pair.agent_info.done]
        echo = None
        if shapes[1] == (20,):
            matches = sum(np.array_equal(np.asarray(pair.agent_info.observations[1].float_data.data)[15:19],
                                         np.asarray(pair.action_info.continuous_actions)) for pair in rows)
            echo = matches / len(rows) if rows else 0
            if echo < .98:
                raise ValueError('Raw labels do not match previous commands; recorder version changed or already aligned')
        elif not marked_previous:
            raise ValueError('16-sensor raw recordings require the collector action-semantics manifest')
        aligned = align_pairs(pairs, include_failures=args.include_failures)
        if shapes[1] == (20,):
            for pair in aligned:
                observation = pair.agent_info.observations[1]
                values = list(observation.float_data.data)
                observation.float_data.data[:] = values[:15] + [values[19]]
                observation.shape[:] = [16]
        raw = path.read_bytes()
        length, position = _DecodeVarint32(raw, 0)
        metadata = DemonstrationMetaProto.FromString(raw[position:position + length])
        length, position = _DecodeVarint32(raw, INITIAL_POS)
        brain = BrainParametersProto.FromString(raw[position:position + length])
        metadata.number_steps = len(aligned)
        metadata.number_episodes = sum(pair.agent_info.done for pair in aligned)
        metadata.mean_reward = sum(pair.agent_info.reward for pair in aligned) / metadata.number_episodes
        write_demo(str(out / path.name), metadata, brain, aligned)
        item = dict(file=path.name, source_sha256=hashlib.sha256(raw).hexdigest(),
                    frames=len(aligned), episodes=metadata.number_episodes,
                    excluded_episodes=sum(pair.agent_info.done for pair in pairs) - metadata.number_episodes,
                    raw_previous_command_match=echo)
        report['files'].append(item)
        print(json.dumps(item), flush=True)
    (out / 'alignment.json').write_text(json.dumps(report, indent=2) + '\n')


if __name__ == '__main__':
    main()
