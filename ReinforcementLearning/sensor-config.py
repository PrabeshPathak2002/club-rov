"""Create a run-local sensor configuration; demo paths are machine-local, not versioned."""
import argparse
import json
from pathlib import Path
import yaml

def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--output', required=True)
    parser.add_argument('--demos')
    parser.add_argument('--max-steps', type=int)
    parser.add_argument('--hold-practice', action='store_true')
    options = parser.parse_args()
    config = yaml.safe_load(Path(__file__).with_name('pool-course-sensors.yaml').read_text())
    behavior = config['behaviors']['PoolCourseSensors']
    if options.hold_practice:
        behavior['network_settings']['memory']['sequence_length'] = 256
        behavior['time_horizon'] = 256
        config['environment_parameters'].update(practice_hover_probability=.5,
            practice_dock_probability=.2, practice_position_radius=.3,
            practice_depth_radius=.1, practice_heading_range=10, practice_approach_distance=2.0,
            practice_dock_clearance=.5,
            practice_timeout_seconds=30)
    if options.demos:
        path = Path(options.demos).resolve()
        if not path.exists() or (path.is_dir() and not list(path.glob('*.demo'))):
            parser.error('Demonstration path contains no .demo files')
        if not (path if path.is_dir() else path.parent).joinpath('alignment.json').is_file():
            parser.error('Use aligned sensor demonstrations, produced by align-sensor-demos.py')
        manifest = json.loads((path if path.is_dir() else path.parent).joinpath('alignment.json').read_text())
        if manifest.get('vector_observation_size') != 16 or manifest.get('output_action_semantics') != 'next_action':
            parser.error('Use the current aligned 16-sensor demonstration format')
        behavior['behavioral_cloning'] = dict(demo_path=str(path), strength=1.0, steps=3000000 if options.hold_practice else 500000,
                                              batch_size=256, num_epoch=3, samples_per_update=4096)
    if options.max_steps is not None:
        if options.max_steps <= 0:
            parser.error('max-steps must be positive')
        behavior['max_steps'] = options.max_steps
        behavior['summary_freq'] = min(10000, options.max_steps)
        behavior['checkpoint_interval'] = min(100000, options.max_steps)
    output = Path(options.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(yaml.safe_dump(config, sort_keys=False))

if __name__ == '__main__':
    main()
