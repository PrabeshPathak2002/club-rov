import importlib.util
from pathlib import Path
import unittest
import os
import signal
import subprocess
import sys
import tempfile
import time

spec = importlib.util.spec_from_file_location('cpu_budget', Path(__file__).with_name('cpu-budget.py'))
budget = importlib.util.module_from_spec(spec)
spec.loader.exec_module(budget)


class BudgetTests(unittest.TestCase):
    @unittest.skipUnless(os.name == 'posix', 'POSIX terminal signal handling')
    def test_background_terminal_stop_saves_child(self):
        with tempfile.TemporaryDirectory() as folder:
            ready, saved = Path(folder) / 'ready', Path(folder) / 'saved'
            child = ('import os,time\nfrom pathlib import Path\n'
                     f'Path({str(ready)!r}).write_text(str(os.getpid()))\n'
                     'try:\n while True: time.sleep(.1)\n'
                     f'except KeyboardInterrupt: Path({str(saved)!r}).write_text("saved")\n')
            process = subprocess.Popen([sys.executable, str(Path(__file__).with_name('cpu-budget.py')),
                '--log', str(Path(folder) / 'cpu.jsonl'), '--no-throttle', '--', sys.executable, '-c', child],
                preexec_fn=lambda: signal.signal(signal.SIGINT, signal.SIG_IGN),
                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            try:
                deadline = time.monotonic() + 10
                while not ready.exists() and time.monotonic() < deadline:
                    time.sleep(.05)
                self.assertTrue(ready.exists())
                process.send_signal(signal.SIGTERM)
                process.wait(timeout=20)
                self.assertEqual(process.returncode, 0)
                self.assertTrue(saved.exists())
            finally:
                if process.poll() is None:
                    if ready.exists():
                        try: os.kill(int(ready.read_text()), signal.SIGTERM)
                        except ProcessLookupError: pass
                    process.kill()
                    process.wait()
    def test_explicit_max_performance_never_throttles(self):
        self.assertFalse(budget.should_pause(100, False, enabled=False))
        self.assertFalse(budget.should_pause(100, True, enabled=False))
    def test_headroom_and_hysteresis(self):
        paused = False
        states = []
        for cpu in (70, 74, 75, 72, 61, 60, 70):
            paused = budget.should_pause(cpu, paused)
            states.append(paused)
        self.assertEqual(states, [False, False, True, True, True, False, False])

    def test_invalid_samples_cannot_resume_training(self):
        for cpu in (float('nan'), float('inf'), -1, 101):
            with self.assertRaises(ValueError):
                budget.should_pause(cpu, True)


if __name__ == '__main__':
    unittest.main()
