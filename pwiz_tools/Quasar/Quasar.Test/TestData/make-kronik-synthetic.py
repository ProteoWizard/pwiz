"""Writes kronik-synthetic.hk, a made-up Hardklor result for testing Glimpse's Kronik port.

The expected features come from the real BullseyeSharp (Skyline's build), run on this file:

    python make-kronik-synthetic.py
    BullseyeSharp.exe kronik-synthetic.hk none.mzML none.pos none.neg   # writes kronik-synthetic.hk.bs.kro

BullseyeSharp writes the linked features before it tries (and fails) to read none.mzML for MS2 matching.
Fixed seed: rerunning gives the same file.
"""
import random

rng = random.Random(20261122)
SCANS = 120
lines_by_scan = [[] for _ in range(SCANS)]


def add(scan, mass, charge, intensity):
    lines_by_scan[scan].append((mass, charge, intensity))


# Persistent features: a mass and charge seen over a run of scans, sometimes missing one or two scans.
for _ in range(60):
    mass = rng.uniform(600, 4000)
    charge = rng.randint(1, 5)
    start = rng.randint(0, SCANS - 3)
    length = rng.randint(1, 15)
    apex = rng.uniform(1e5, 1e8)
    for k in range(length):
        scan = start + k
        if scan >= SCANS:
            break
        if length > 3 and rng.random() < 0.15:     # a missed scan
            continue
        shape = 1.0 - abs(k - length / 2) / (length / 2 + 1)
        jitter = rng.uniform(-6, 6) * mass / 1e6     # within 10 ppm of the apex mass
        add(scan, mass + jitter, charge, apex * shape * rng.uniform(0.8, 1.2))

# Near misses: the same mass at another charge, or just outside 10 ppm, next to a feature.
for _ in range(15):
    mass = rng.uniform(800, 3000)
    start = rng.randint(0, SCANS - 6)
    for k in range(5):
        add(start + k, mass, 2, 1e6 * (k + 1))
        add(start + k, mass, 3, 9e5 * (k + 1))                      # same mass, other charge
        add(start + k, mass * (1 + 14e-6 * (1 if k % 2 else -1)), 2, 5e5)  # 14 ppm away

# Noise: single envelopes anywhere.
for scan in range(SCANS):
    for _ in range(rng.randint(5, 25)):
        add(scan, rng.uniform(600, 4000), rng.randint(1, 6), rng.uniform(1e3, 1e6))

with open("kronik-synthetic.hk", "w", newline="\n") as f:
    for scan in range(SCANS):
        rt = 10 + scan * 0.01
        f.write(f"S\t{scan + 1}\t{rt:.4f}\tsynthetic.mzML\n")
        for mass, charge, intensity in lines_by_scan[scan]:
            mz = (mass + charge * 1.00727649) / charge
            # P, mass, charge, intensity, base peak m/z, m/z window, unused, modifications, correlation
            f.write(f"P\t{mass:.4f}\t{charge}\t{intensity:.1f}\t{mz:.4f}\t{mz - 1:.4f}-{mz + 3:.4f}\t0.0000\t_\t0.9500\n")
