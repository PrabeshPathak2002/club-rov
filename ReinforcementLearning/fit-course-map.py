"""Learn a nominal landmark map from demonstration labels, anchored at gate one.

The saved prior is learned offline. Runtime registers it using camera/DVL data;
it never reads Unity target transforms. Changed layouts require separate tests.
"""
import argparse
import json
from pathlib import Path
import math
import numpy as np


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--data', nargs='+', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    maps = []
    for folder in args.data:
        with np.load(Path(folder)/'landmark-supervision.npz') as data:
            starts = np.flatnonzero(data['starts'])
            landmarks = data['landmarks'][starts]
        for row in landmarks:
            angle = -math.atan2(float(row[0, 3]), float(row[0, 4]))
            c, s = math.cos(angle), math.sin(angle)
            matrix = np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
            maps.append((matrix @ (row[:, :3]-row[0, :3]).T).T)
    values = np.asarray(maps)
    offsets = np.median(values, axis=0)
    out = Path(args.output)
    out.parent.mkdir(parents=True, exist_ok=True)
    np.savez(out, offsets=offsets.astype(np.float32))
    report = dict(mode='demonstration_learned_course_map_prior', episodes=len(maps),
                  anchor='first visually observed gate', median_absolute_deviation_m=float(np.median(np.abs(values-offsets))),
                  generalisation_verified=False)
    out.with_suffix('.json').write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps(report), flush=True)


if __name__ == '__main__': main()
