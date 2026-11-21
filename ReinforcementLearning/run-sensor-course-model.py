"""Run a validated learned-geometry course policy with a live camera preview."""
import argparse
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import io
import json
from pathlib import Path
import shlex
import signal
import subprocess
import tempfile
import threading

import numpy as np
from PIL import Image
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.side_channel.environment_parameters_channel import EnvironmentParametersChannel
from mlagents_envs.side_channel.stats_side_channel import StatsSideChannel
from tracked_geometry import TrackedGeometry, runtime_fingerprint


PAGE = b'''<!doctype html><html><head><title>ClubROV sensor course model</title>
<style>body{background:#081b25;color:#e7f4f5;font:18px system-ui;margin:30px}main{max-width:1050px;margin:auto}
img{width:840px;max-width:100%;image-rendering:pixelated;border:1px solid #39848b}h1{font-size:26px}
button{padding:10px 22px;font:inherit;background:#e9bb3c;border:0;border-radius:6px;cursor:pointer}
pre{white-space:pre-wrap;color:#b5d2d8}</style></head><body><main>
<h1>ClubROV | sensor-only course policy</h1><p id="task">Starting the simulator...</p>
<img id="camera" alt="ROV forward camera"><p id="score"></p><button onclick="fetch('/stop',{method:'POST'})">Stop model</button>
<pre id="detail"></pre><p>Task stage is estimated from camera and motion sensors. Completion is scored independently by the simulator.</p>
</main><script>let stages=['Gate 1','Gate 2','Gate 3','Hover hold','Dock hold'];
async function refresh(){try{let s=await(await fetch('/status')).json();
document.getElementById('task').textContent='Estimated task: '+(stages[s.phase]||'initializing');
document.getElementById('score').textContent='Full courses completed: '+s.completed+' / '+s.episodes;
document.getElementById('detail').textContent=s.message||'';
document.getElementById('camera').src='/frame.jpg?t='+Date.now();}catch(e){}setTimeout(refresh,150)}refresh();</script></body></html>'''


class Preview:
    def __init__(self, port):
        self.stop = threading.Event()
        self.frame = b''
        self.status = dict(phase=0, episodes=0, completed=0, message='Loading model')
        owner = self
        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *args): pass
            def do_GET(self):
                if self.path.startswith('/frame.jpg'):
                    data, kind = owner.frame, 'image/jpeg'
                elif self.path == '/status':
                    data, kind = json.dumps(owner.status).encode(), 'application/json'
                else:
                    data, kind = PAGE, 'text/html'
                self.send_response(200)
                self.send_header('Content-Type', kind)
                self.send_header('Cache-Control', 'no-store')
                self.end_headers()
                try: self.wfile.write(data)
                except (BrokenPipeError, ConnectionResetError): pass
            def do_POST(self):
                if self.path == '/stop': owner.stop.set()
                self.send_response(204)
                self.end_headers()
        self.server = ThreadingHTTPServer(('127.0.0.1', port), Handler)
        threading.Thread(target=self.server.serve_forever, daemon=True).start()

    def publish(self, image, status):
        buffer = io.BytesIO()
        Image.fromarray((image.transpose(1, 2, 0).clip(0, 1)*255).astype(np.uint8)).save(buffer, 'JPEG', quality=90)
        self.frame, self.status = buffer.getvalue(), status


def main():
    def interrupted(*_): raise KeyboardInterrupt
    for name in ('SIGINT', 'SIGTERM', 'SIGHUP'):
        if hasattr(signal, name): signal.signal(getattr(signal, name), interrupted)
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--policy', default=str(Path(__file__).with_name('Models')/'SensorCourseGeometry.onnx'))
    parser.add_argument('--episodes', type=int, default=0, help='0 repeats until stopped')
    parser.add_argument('--speed', type=float, default=1)
    parser.add_argument('--profile', choices=['fixed', 'randomised'], default='fixed')
    parser.add_argument('--seed', type=int, default=41000)
    parser.add_argument('--port', type=int, default=8766)
    parser.add_argument('--base-port', type=int, default=8500)
    parser.add_argument('--no-preview', action='store_true')
    args = parser.parse_args()
    if not Path(args.policy).is_file(): parser.error('Validated model missing; see ReinforcementLearning/README.md')
    if not Path(args.policy).with_name('CourseMap.npz').is_file():
        parser.error('CourseMap.npz is required beside the learned ONNX model')
    if args.episodes < 0 or not 0 < args.speed <= 30: parser.error('Invalid episode count or simulation speed')
    model = TrackedGeometry(args.policy)
    preview = None if args.no_preview else Preview(args.port)
    if preview:
        print(f'Live camera: http://127.0.0.1:{args.port}/', flush=True)
        subprocess.Popen(['powershell.exe', '-NoProfile', '-Command', f"Start-Process 'http://127.0.0.1:{args.port}/'"],
                         stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    out = Path.home()/'club-rov-rl-results'/('live-sensor-course-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ'))
    out.mkdir(parents=True, exist_ok=False)
    engine, parameters, stats = EngineConfigurationChannel(), EnvironmentParametersChannel(), StatsSideChannel()
    engine.set_configuration_parameters(time_scale=args.speed, target_frame_rate=60 if args.speed == 1 else -1)
    parameters.set_float_parameter('course_randomisation', int(args.profile == 'randomised'))
    parameters.set_float_parameter('practice_hover_probability', 0)
    parameters.set_float_parameter('practice_dock_probability', 0)
    player = Path(__file__).resolve().parents[1]/'Simulation/ClubROVSim/Builds/SensorTraining/ClubROVSim.exe'
    results, objectives, teacher_samples = [], [], 0
    stop_reason, failure = 'episode_limit', None
    with tempfile.TemporaryDirectory(prefix='rov-live-policy-') as directory:
        wrapper = Path(directory)/'LivePolicy.x86_64'
        wrapper.write_text('#!/usr/bin/env bash\nargs=()\nwhile (($#)); do\n'
            'if [[ $1 == -logFile && $# -ge 2 ]]; then args+=(-logFile "$(wslpath -w "$2")"); shift 2\n'
            'else args+=("$1"); shift; fi\ndone\nexec '+shlex.quote(str(player))+' "${args[@]}" > '+
            shlex.quote(str(out/'player-console.log'))+' 2>&1\n')
        wrapper.chmod(0o700)
        env = UnityEnvironment(file_name=str(wrapper.with_suffix('')), seed=args.seed, base_port=args.base_port,
            side_channels=[engine, parameters, stats], log_folder=str(out), timeout_wait=120,
            additional_args=['-batchmode','-job-worker-count','1','--rov-rl-sensors'])
        try:
            env.reset()
            name, spec = next(iter(env.behavior_specs.items()))
            if not name.startswith('PoolCourseSensors?') or [tuple(o.shape) for o in spec.observation_specs] != [(3,64,84),(16,)]:
                raise RuntimeError('Sensor observation contract mismatch')
            while not args.episodes or len(results) < args.episodes:
                if preview and preview.stop.is_set():
                    stop_reason = 'preview_stop'
                    break
                decisions, terminals = env.get_steps(name)
                samples = stats.get_and_reset_stats()
                teacher_values = samples.get('Sensor/TeacherActive', [])
                teacher_samples += len(teacher_values)
                if any(value != 0 for value, _ in teacher_values): raise RuntimeError('Teacher active during inference')
                results.extend(value for value, _ in samples.get('Course/Completed', []))
                objectives.extend(value for value, _ in samples.get('Course/Objectives', []))
                if len(terminals):
                    print(f'Course {len(results)}: {"COMPLETE" if results and results[-1] else "not completed"}; '
                          f'objectives {objectives[-1] if objectives else "unknown"}/5', flush=True)
                    model.reset()
                    if preview:
                        preview.status = dict(phase=4, completed=sum(results), episodes=len(results), message='Last course scored: '+('COMPLETE' if results and results[-1] else 'not completed'))
                if args.episodes and len(results) >= args.episodes: break
                if len(decisions):
                    image, sensors = decisions.obs[0][0], decisions.obs[1][0]
                    action = model.predict(image, sensors)
                    env.set_actions(name, ActionTuple(continuous=action[None]))
                    if preview:
                        preview.publish(image, dict(phase=model.phase, completed=sum(results), episodes=len(results),
                            message=f'Estimated hold: {model.hold:.1f}s | target locked: {model.locked}'))
                env.step()
        except KeyboardInterrupt:
            stop_reason = 'interrupt'
            print('Stopping sensor model.', flush=True)
        except Exception as error:
            stop_reason, failure = 'error', str(error)
            raise
        finally:
            env.close()
            if preview: preview.server.shutdown()
            report = dict(policy=args.policy, profile=args.profile, seed=args.seed, episodes=len(results),
                completed=sum(results), objectives=objectives, teacher_absence_samples=teacher_samples,
                stop_reason=stop_reason, error=failure,
                navigation_runtime=runtime_fingerprint(args.policy))
            (out/'results.json').write_text(json.dumps(report, indent=2)+'\n')
            print(json.dumps(report), flush=True)
            print('Run report:', out, flush=True)


if __name__ == '__main__': main()
