"""Warm-start the sensor actor from recorded observations, without simulator rollouts.

This is behavioural cloning, not a course-completion evaluation. Outputs remain
compatible with start-sensor-training.sh --initialize-from=<output run ID>.
"""
import argparse
import hashlib
import json
from pathlib import Path
import time

import numpy as np
import yaml
from mlagents.torch_utils import torch
from mlagents.plugins.trainer_type import register_trainer_plugins
from mlagents.trainers.demo_loader import load_demonstration
from mlagents.trainers.model_saver.torch_model_saver import TorchModelSaver
from mlagents.trainers.policy.torch_policy import TorchPolicy
from mlagents.trainers.settings import BehavioralCloningSettings, RunOptions
from sensor_bc import SensorBCModule as BCModule, repair_legacy_action_head
from mlagents.trainers.torch_entities.networks import SimpleActor


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--demos', required=True)
    parser.add_argument('--checkpoint', help='Optional compatible sensor actor checkpoint; otherwise start fresh')
    parser.add_argument('--output', required=True, help='New empty run directory under RL results')
    parser.add_argument('--updates', type=int, default=100)
    parser.add_argument('--seed', type=int, default=119)
    parser.add_argument('--sequence-length', type=int, choices=[64, 128, 256], help='Longer imitation context; does not change exported memory shape')
    parser.add_argument('--repair-legacy-action-scale', action='store_true', help='One-time conversion of old incorrectly scaled BC checkpoints')
    args = parser.parse_args()
    if not (Path(args.demos) / 'alignment.json').is_file():
        parser.error('Use aligned sensor demonstrations, produced by align-sensor-demos.py')
    if args.updates < 1:
        parser.error('updates must be positive')
    if args.repair_legacy_action_scale:
        if not args.checkpoint:
            parser.error('Action-scale repair requires an old checkpoint')
        source_report = Path(args.checkpoint).parent.parent / 'cloning-report.json'
        if not source_report.is_file() or json.loads(source_report.read_text()).get('bc_action_space') == 'ppo_raw':
            parser.error('Repair requires a legacy cloning report; never repair a corrected checkpoint twice')
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=False)
    torch.set_num_threads(1)
    torch.set_num_interop_threads(1)
    torch.manual_seed(args.seed)
    np.random.seed(args.seed)
    register_trainer_plugins()
    config = RunOptions.from_dict(yaml.safe_load(Path(__file__).with_name('pool-course-sensors.yaml').read_text()))
    settings = config.behaviors['PoolCourseSensors']
    if args.sequence_length:
        settings.network_settings.memory.sequence_length = args.sequence_length
    demo_files = sorted(Path(args.demos).glob('*.demo'))
    if not demo_files:
        parser.error('No demonstration files')
    spec, _, _ = load_demonstration(str(demo_files[0]))
    if [tuple(item.shape) for item in spec.observation_specs] != [(3, 64, 84), (16,)]:
        raise ValueError('Demonstrations do not match sensor actor contract')
    policy = TorchPolicy(args.seed, spec, settings.network_settings, SimpleActor,
                         dict(conditional_sigma=False, tanh_squash=False))
    if args.checkpoint:
        state = torch.load(args.checkpoint)
        policy.actor.load_state_dict(state['Policy'], strict=True)
    if args.repair_legacy_action_scale:
        repair_legacy_action_head(policy.actor)
    policy.set_step(0)
    bc = BCModule(policy, BehavioralCloningSettings(demo_path=str(Path(args.demos).resolve()),
                  strength=1.0, steps=0, batch_size=256, num_epoch=1, samples_per_update=4096),
                  settings.hyperparameters.learning_rate, 256, 1)
    if not args.checkpoint:
        policy.actor.update_normalization(bc.demonstration_buffer)
    losses = []
    started = time.monotonic()
    saver = TorchModelSaver(settings, str(out / 'PoolCourseSensors'))
    saver.register(policy)
    try:
        for update in range(args.updates):
            loss = float(bc.update()['Losses/Pretraining Loss'])
            if not np.isfinite(loss):
                raise ValueError('Nonfinite behavioural-cloning loss')
            losses.append(loss)
            if (update + 1) % 10 == 0:
                print(f'BC update {update + 1}/{args.updates}: loss={loss:.5f}, '
                      f'elapsed={time.monotonic() - started:.1f}s', flush=True)
    except KeyboardInterrupt:
        print('Saving interrupted cloning warm-start.', flush=True)
    finally:
        model, _ = saver.save_checkpoint('PoolCourseSensors', 0)
        report = dict(mode='offline_sensor_behavioural_cloning', updates=len(losses),
                      bc_action_space='ppo_raw', legacy_action_scale_repaired=args.repair_legacy_action_scale,
                      sequence_length=policy.sequence_length, losses=losses,
                      seconds=time.monotonic() - started, seed=args.seed,
                      source_sha256=hashlib.sha256(Path(args.checkpoint).read_bytes()).hexdigest() if args.checkpoint else None,
                      demos={path.name: hashlib.sha256(path.read_bytes()).hexdigest() for path in demo_files},
                      model=model, course_completion=None)
        (out / 'cloning-report.json').write_text(json.dumps(report, indent=2) + '\n')


if __name__ == '__main__':
    main()
