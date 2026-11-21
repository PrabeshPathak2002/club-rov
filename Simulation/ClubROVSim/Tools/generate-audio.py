"""Create original, seamless mono PCM loops; standard-library only, no recordings."""
import math
import random
import struct
import wave
from pathlib import Path

RATE = 22050
DEST = Path(__file__).resolve().parents[1] / 'Assets/ROV/Resources'


def write(name, seconds, motor):
    rng = random.Random(650 if motor else 2412)
    count = int(seconds * RATE)
    samples = [0.0] * count
    # Integer cycle counts make every component periodic at the loop boundary.
    for _ in range(90):
        freq = math.exp(rng.uniform(math.log(30), math.log(1500 if motor else 550)))
        cycles = max(1, round(freq * seconds))
        amplitude = (cycles / seconds / 80) ** -0.55
        phase = rng.uniform(0, math.tau)
        delta = math.tau * cycles / count
        for i in range(count):
            samples[i] += amplitude * math.sin(delta * i + phase)
    for i in range(count):
        t = i / RATE
        if motor:
            samples[i] = samples[i] * 0.065 + 0.45 * math.sin(math.tau * 96 * t) + 0.18 * math.sin(math.tau * 192 * t)
            samples[i] *= 0.9 + 0.1 * math.sin(math.tau * 12 * t)
        else:
            samples[i] *= 0.8 + 0.2 * math.sin(math.tau * t / seconds)
    peak = max(abs(v) for v in samples)
    pcm = b''.join(struct.pack('<h', round(v / peak * 24000)) for v in samples)
    DEST.mkdir(parents=True, exist_ok=True)
    with wave.open(str(DEST / name), 'wb') as out:
        out.setparams((1, 2, RATE, count, 'NONE', 'not compressed'))
        out.writeframes(pcm)
    print(f'{name}: {seconds}s, mono {RATE}Hz, peak {24000 / 32768:.3f}')


if __name__ == '__main__':
    write('RovMotor.wav', 2, True)
    write('RovAmbience.wav', 8, False)
