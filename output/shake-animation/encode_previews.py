"""Encode unchanged native Unity frames as animated WebP previews."""
from pathlib import Path
from PIL import Image, features
import json
import argparse

parser = argparse.ArgumentParser()
parser.add_argument("--player-only", action="store_true")
args = parser.parse_args()

root = Path(__file__).resolve().parent
if not features.check("webp"):
    raise RuntimeError("WebP support is required.")

records = []
for owner in (("player",) if args.player_only else ("player", "boss")):
    paths = sorted((root / f"{owner}-smooth-frames").glob("Frame-*.png"))
    if len(paths) < 100:
        raise RuntimeError(f"Incomplete native animation frames: {owner}: {len(paths)}")
    frames = [Image.open(path).convert("RGB") for path in paths]
    durations = [round((index + 1) * 1000 / 24) - round(index * 1000 / 24) for index in range(len(frames))]
    durations[-1] = 500
    destination = root / ("Player-RightSide-ColaShake.webp" if args.player_only else
                          ("Player-OriginalFPS-ColaShake.webp" if owner == "player" else "Boss-OriginalModel-ColaShake.webp"))
    frames[0].save(destination, save_all=True, append_images=frames[1:], duration=durations,
                   loop=0, lossless=False, quality=85, method=1)
    with Image.open(destination) as animation:
        encoded_duration = 0
        for frame_index in range(animation.n_frames):
            animation.seek(frame_index)
            animation.load()
            encoded_duration += animation.info.get("duration", 0)
        if encoded_duration != sum(durations) or animation.info.get("loop") != 0:
            raise RuntimeError(f"Preview timing or loop changed: {owner}")
        records.append({"owner": owner, "file": destination.name, "source": f"{owner}-smooth-frames/Frame-*.png",
                        "source_frames": len(paths), "encoded_frames": animation.n_frames,
                        "size": list(animation.size), "clip_seconds": 4.4,
                        "fps": 24, "last_frame_hold_ms": 500,
                        "bytes": destination.stat().st_size, "lossless": False, "quality": 85,
                        "encoded_duration_ms": encoded_duration, "loop": 0})
    for frame in frames:
        frame.close()
    (root / f"{owner.capitalize()}-ColaShake.webp").write_bytes(destination.read_bytes())
    (root / f"{owner.capitalize()}-OwnHand-ColaShake.webp").write_bytes(destination.read_bytes())
    if args.player_only:
        (root / "Player-OriginalFPS-ColaShake.webp").write_bytes(destination.read_bytes())
    print(f"{owner}: {len(paths)} native frames encoded, {destination.stat().st_size} bytes", flush=True)
(root / ("Player-RightSide-encoding.json" if args.player_only else "Preview-encoding.json")).write_text(json.dumps(records, indent=2), encoding="utf-8")
