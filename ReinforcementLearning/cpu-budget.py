"""Throttle only this trainer's process group against Windows total CPU usage."""
import argparse
import json
import math
import os
import signal
import subprocess
import time
from pathlib import Path


def should_pause(cpu, paused, enabled=True):
    if not enabled:
        return False
    if not math.isfinite(cpu) or not 0 <= cpu <= 100:
        raise ValueError('Windows CPU sample must be finite and between 0 and 100')
    return cpu >= 75 if not paused else cpu > 60


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--log', required=True)
    parser.add_argument('--no-throttle', action='store_true', help='Monitor CPU without pausing training')
    parser.add_argument('command', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ['--'] else args.command
    if not command:
        parser.error('a trainer command is required')
    Path(args.log).parent.mkdir(parents=True, exist_ok=True)
    def request_stop(signum, frame):
        raise KeyboardInterrupt
    # A closed terminal or background-job stop should save, just like Ctrl+C.
    for stop_signal in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP):
        signal.signal(stop_signal, request_stop)
    # Install before spawning: an asynchronous shell can inherit SIGINT ignored.
    # The child must start with its normal KeyboardInterrupt handler, not SIG_IGN.
    process = subprocess.Popen(command, start_new_session=True)
    paused = False
    try:
        with open(args.log, 'a', buffering=1) as log:
            while process.poll() is None:
                try:
                    sample = subprocess.check_output([
                        'powershell.exe', '-NoProfile', '-NonInteractive', '-Command',
                        "(Get-CimInstance Win32_PerfFormattedData_PerfOS_Processor -Filter \"Name='_Total'\").PercentProcessorTime"
                    ], timeout=10, text=True)
                    cpu = float(sample.strip())
                    next_paused = should_pause(cpu, paused, not args.no_throttle)
                    error = None
                except (OSError, subprocess.SubprocessError, ValueError) as exc:
                    cpu, next_paused, error = None, not args.no_throttle, str(exc)
                if next_paused != paused and process.poll() is None:
                    os.killpg(process.pid, signal.SIGSTOP if next_paused else signal.SIGCONT)
                    paused = next_paused
                log.write(json.dumps(dict(time=time.time(), cpu=cpu, paused=paused, error=error)) + '\n')
                time.sleep(1)
    except KeyboardInterrupt:
        if process.poll() is None:
            os.killpg(process.pid, signal.SIGCONT)
            os.killpg(process.pid, signal.SIGINT)
            process.wait()
    finally:
        if process.poll() is None:
            os.killpg(process.pid, signal.SIGCONT)
            os.killpg(process.pid, signal.SIGINT)
            process.wait()
    raise SystemExit(process.returncode)


if __name__ == '__main__':
    main()
