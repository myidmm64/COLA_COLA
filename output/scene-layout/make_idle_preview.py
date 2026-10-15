"""Package actual Unity Play-mode captures as a preview; no generated/interpolated frames."""
from pathlib import Path
import sys
from PIL import Image

root = Path(__file__).resolve().parent
times = [float(value) for value in (root / "Idle-frame-times.txt").read_text().splitlines()]
source = [Image.open(root / "idle-frames" / f"Idle-{i:03}.png").convert("RGB") for i in range(len(times))]
palette = source[0].quantize(colors=256, method=Image.Quantize.MEDIANCUT)
frames = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in source]
durations = [max(20, round((end - start) * 100) * 10) for start, end in zip(times, times[1:])]
durations.append(durations[-1])
target = root / (sys.argv[1] if len(sys.argv) > 1 else "MainScene-idle-preview.gif")
frames[0].save(target, save_all=True, append_images=frames[1:], duration=durations, loop=0, optimize=True, disposal=1)
print(f"{target}: {len(frames)} actual Play-mode frames, {sum(durations) / 1000:.2f}s, {target.stat().st_size:,} bytes")
