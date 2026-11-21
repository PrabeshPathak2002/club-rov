"""Combine aligned full-course examples with weighted corrective demonstrations."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--base', required=True)
    parser.add_argument('--corrections', nargs='+', required=True)
    parser.add_argument('--copies', type=int, default=3)
    parser.add_argument('--base-copies', type=int, default=1)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    if not 1 <= args.copies <= 8 or not 1 <= args.base_copies <= 8:
        parser.error('copies must be 1..8')
    sources = []
    for index, source in enumerate([args.base] + args.corrections):
        folder = Path(source)
        manifest = json.loads((folder / 'alignment.json').read_text())
        if manifest.get('schema') != 2 or manifest.get('vector_observation_size') != 16 or manifest.get('output_action_semantics') != 'next_action':
            parser.error('Every input must already use aligned sensor16 next-action labels')
        if index and manifest.get('collection_teacher') != 'hold_feedback_above_dock_v1':
            parser.error('Use validated feedback corrections, not failed legacy expert recordings')
        files = list(folder.glob('*.demo'))
        if not files:
            parser.error('Input contains no demonstrations')
        sources.append((folder, manifest, files, args.base_copies if index == 0 else args.copies))
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=False)
    report = dict(schema=2, vector_observation_size=16, output_action_semantics='next_action',
                  dataset_kind='full_courses_and_feedback_corrections', successful_only=False, files=[], sources=[])
    for index, (folder, manifest, files, copies) in enumerate(sources):
        report['sources'].append(dict(folder=str(folder), copies=copies, manifest=manifest))
        for source in files:
            for number in range(copies):
                name = f'source{index}-{number}-{source.name}'
                shutil.copy2(source, out / name)
                report['files'].append(dict(file=name, sha256=hashlib.sha256(source.read_bytes()).hexdigest()))
    (out / 'alignment.json').write_text(json.dumps(report, indent=2) + '\n')
    print(f'Combined {len(report["files"])} aligned demonstration files.', flush=True)


if __name__ == '__main__':
    main()
