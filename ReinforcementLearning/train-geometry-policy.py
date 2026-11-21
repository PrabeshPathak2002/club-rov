"""Supervise target-state estimates; inference remains RGB plus measured sensors."""
import argparse
import json
from pathlib import Path
import random
import time

import numpy as np
import torch
from torch.nn import functional as F
from torch.utils.tensorboard import SummaryWriter
from geometry_policy import GeometryPolicy, GoalGeometryPolicy, WorldGeometryPolicy


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--data', nargs='+', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--updates', type=int, default=5000)
    parser.add_argument('--batch-size', type=int, default=4)
    parser.add_argument('--checkpoint')
    parser.add_argument('--goal-conditioned', action='store_true')
    parser.add_argument('--world-map', action='store_true')
    args = parser.parse_args()
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=False)
    torch.set_num_threads(1)
    torch.manual_seed(301)
    rng = random.Random(301)
    episodes = []
    for folder in args.data:
        filename = 'landmark-supervision.npz' if args.world_map else 'geometry-supervision.npz'
        keys = ('images', 'sensors', 'targets', 'starts', 'landmarks') if args.world_map else ('images', 'sensors', 'targets', 'starts')
        with np.load(Path(folder) / filename) as source:
            data = {key: source[key] for key in keys}
        boundaries = np.r_[np.flatnonzero(data['starts']), len(data['starts'])]
        for start, end in zip(boundaries[:-1], boundaries[1:]):
            if end - start <= 512:
                episodes.append((data['images'][start:end].copy(), data['sensors'][start:end].copy(),
                                 data['targets'][start:end].copy(),
                                 data['landmarks'][start:end].copy() if args.world_map else None))
    if len(episodes) < 8:
        raise ValueError('Need at least eight complete labelled episodes')
    rng.shuffle(episodes)
    validation, training = episodes[:max(4, len(episodes)//10)], episodes[max(4, len(episodes)//10):]
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    if args.world_map:
        args.goal_conditioned = True
    model = (WorldGeometryPolicy() if args.world_map else GoalGeometryPolicy() if args.goal_conditioned else GeometryPolicy()).to(device)
    if args.checkpoint:
        saved = torch.load(args.checkpoint, map_location=device)['model']
        if args.world_map:
            saved = {key: value for key, value in saved.items() if not key.startswith('state.')}
            model.load_state_dict(saved, strict=False)
        elif args.goal_conditioned and saved['state.0.weight'].shape[1] == 64:
            saved['state.0.weight'] = F.pad(saved['state.0.weight'], (0, 5))
            model.load_state_dict(saved)
        else:
            model.load_state_dict(saved)
    optimizer = torch.optim.Adam(model.parameters(), lr=3e-4)
    writer = SummaryWriter(str(out / 'GeometryPolicy'))
    started = time.monotonic()

    def batch(rows):
        length = max(len(row[0]) for row in rows)
        images = np.zeros((len(rows), length, 3, 64, 84), np.uint8)
        sensors = np.zeros((len(rows), length, 16), np.float32)
        targets = np.zeros((len(rows), length, 6), np.float32)
        mask = np.zeros((len(rows), length), np.float32)
        landmarks = np.zeros((len(rows), length, 5, 5), np.float32)
        for i, row in enumerate(rows):
            n = len(row[0])
            images[i, :n], sensors[i, :n], targets[i, :n], mask[i, :n] = *row[:3], 1
            if args.world_map: landmarks[i, :n] = row[3]
        return (torch.from_numpy(images).to(device).float()/255,
                torch.from_numpy(sensors).to(device), torch.from_numpy(targets).to(device),
                torch.from_numpy(mask).to(device), torch.from_numpy(landmarks).to(device))

    def losses(rows):
        images, sensors, targets, mask, landmarks = batch(rows)
        queries = F.one_hot(targets[:, :, 5].long(), 5).float() if args.goal_conditioned else None
        if args.world_map:
            state, phase, _ = model.estimate_all(images, sensors, torch.zeros(len(rows), 1, 136, device=device))
            position = F.smooth_l1_loss(state[:, :, :, :3], landmarks[:, :, :, :3], reduction='none').mean((2, 3))
            heading = (state[:, :, :, 3:] - landmarks[:, :, :, 3:]).square().mean((2, 3))
            absolute_error = (state[:, :, :, :3]-landmarks[:, :, :, :3]).abs().mean((2, 3))
            mask = mask * sensors[:, :, 15]
        else:
            state, phase, _ = model.estimate(images, sensors, torch.zeros(len(rows), 1, 136, device=device), queries)
            position = F.smooth_l1_loss(state[:, :, :3], targets[:, :, :3], reduction='none').mean(2)
            heading = (state[:, :, 3:] - targets[:, :, 3:5]).square().mean(2)
            absolute_error = (state[:, :, :3]-targets[:, :, :3]).abs().mean(2)
        stage = F.cross_entropy(phase.transpose(1, 2), targets[:, :, 5].long(), reduction='none')
        count = mask.sum().clamp_min(1)
        return ((position + heading + stage * .3) * mask).sum()/count, \
            (absolute_error*mask).sum()/count

    try:
        for update in range(1, args.updates+1):
            model.train()
            optimizer.zero_grad()
            loss, position_error = losses(rng.sample(training, min(args.batch_size, len(training))))
            if not torch.isfinite(loss):
                raise RuntimeError('Nonfinite geometry loss')
            loss.backward()
            torch.nn.utils.clip_grad_norm_(model.parameters(), 5)
            optimizer.step()
            for group in optimizer.param_groups:
                group['lr'] = 3e-5 + 2.7e-4*(1-update/args.updates)
            if update % 25 == 0:
                writer.add_scalar('Loss/State', loss.item(), update)
                writer.add_scalar('Position/TrainMAE_m', position_error.item(), update)
            if update % 100 == 0:
                model.eval()
                with torch.no_grad():
                    val_loss, val_error = losses(validation[:4])
                writer.add_scalar('Position/ValidationMAE_m', val_error.item(), update)
                writer.flush()
                print(f'Update {update}/{args.updates}: loss={loss.item():.4f}, '
                      f'position={position_error.item():.3f}m, validation={val_error.item():.3f}m, '
                      f'elapsed={time.monotonic()-started:.1f}s', flush=True)
            if update % 500 == 0:
                torch.save(dict(model=model.state_dict(), update=update), out / f'geometry-{update}.pt')
    except KeyboardInterrupt:
        print('Interrupted; exporting saved candidate.', flush=True)
    finally:
        model.eval().cpu()
        torch.save(dict(model=model.state_dict(), update=update), out/'checkpoint.pt')
        inputs = (torch.zeros(1, 3, 64, 84), torch.zeros(1, 16), torch.zeros(1, 1, 136))
        if args.goal_conditioned:
            inputs += (torch.tensor([[1., 0, 0, 0, 0]]),)
        torch.onnx.export(model, inputs,
            str(out/'GeometryPolicy.onnx'), opset_version=13,
            input_names=['obs_0','obs_1','recurrent_in'] + (['goal_phase'] if args.goal_conditioned else []),
            output_names=['deterministic_continuous_actions','recurrent_out','state_estimate','phase_logits'])
        writer.close()
        (out/'report.json').write_text(json.dumps(dict(mode='sensor_only_learned_geometry_with_feedback',
            training_episodes=len(training), validation_episodes=len(validation), updates=update,
            seconds=time.monotonic()-started, full_course_completion=None), indent=2)+'\n')


if __name__ == '__main__':
    main()
