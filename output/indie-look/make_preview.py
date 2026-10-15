"""Package actual Unity frames without interpolation or added image effects."""
from pathlib import Path
from PIL import Image
import json
import numpy as np

root = Path(__file__).resolve().parent
captures = root.parent / "horror-atmosphere"
times = [float(line) for line in (captures / "frame-times.txt").read_text().splitlines()]
frames = [Image.open(captures / "frames" / f"Horror-{i:03}.png").convert("RGB") for i in range(len(times))]
durations = [max(20, round((b - a) * 1000)) for a, b in zip(times, times[1:])]
durations.append(durations[-1])
preview = root / "Indie-grain-preview.webp"
frames[0].save(preview, format="WEBP", save_all=True, append_images=frames[1:], lossless=True, method=3, duration=durations, loop=0)

# This patch is empty, stationary tabletop, away from the character and bottle shadows.
patches = [np.asarray(frame, dtype=np.float32)[426:448, 438:522] for frame in frames]
differences = [float(np.abs(b - a).mean()) for a, b in zip(patches, patches[1:])]
result = {
    "source": "Unmodified Unity Play-mode camera captures",
    "frame_count": len(frames),
    "duration_seconds": sum(durations) / 1000,
    "stationary_table_patch_mean_frame_difference_8bit": round(float(np.mean(differences)), 4),
    "preview_bytes": preview.stat().st_size,
}
(root / "Preview-validation.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(result))
