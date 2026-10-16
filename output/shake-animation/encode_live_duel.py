"""Encode the unedited actual Play-mode capture with its measured frame times."""
from pathlib import Path
from PIL import Image
import json

root = Path(__file__).resolve().parent
paths = sorted((root / "duel-live-frames").glob("Frame-*.png"))
times = [float(line) for line in (root / "duel-live-frame-times.txt").read_text().splitlines()]
if len(paths) != len(times) or len(paths) < 40 or times[-1] < 8:
    raise RuntimeError("The actual player/boss turn capture is incomplete.")
durations = [max(1, round((times[index + 1] - times[index]) * 1000)) for index in range(len(times) - 1)] + [700]
frames = [Image.open(path).convert("RGB") for path in paths]
destination = root / "Duel-OriginalFPS-LiveBoss.webp"
frames[0].save(destination, save_all=True, append_images=frames[1:], duration=durations,
               loop=0, lossless=False, quality=85, method=1)
with Image.open(destination) as animation:
    actual_duration = 0
    for index in range(animation.n_frames):
        animation.seek(index)
        animation.load()
        actual_duration += animation.info.get("duration", 0)
    if actual_duration != sum(durations):
        raise RuntimeError("Encoded playback duration differs from the actual capture.")
    record = {"file": destination.name, "source": "actual Unity Play mode, original FPS hands and automatic boss turn",
              "source_frames": len(paths), "encoded_frames": animation.n_frames, "size": list(animation.size),
              "captured_seconds": times[-1] - times[0], "encoded_duration_ms": actual_duration,
              "bytes": destination.stat().st_size, "timing_source": "duel-live-frame-times.txt"}
for frame in frames:
    frame.close()
(root / "Live-duel-encoding.json").write_text(json.dumps(record, indent=2), encoding="utf-8")
print(json.dumps(record), flush=True)
