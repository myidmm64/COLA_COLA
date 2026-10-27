# JUST SHAKE. Windows / itch.io build

- Both Unity release builds succeeded with zero build errors.
- Windows ZIP: `Builds/JUST_SHAKE_Windows_x64.zip`
- HTML5 ZIP: `Builds/JUST_SHAKE_itchio.zip`
- Web archive has index.html at root; all files are below 200 MB, file count below 1000.
- Supported-only resolution filtering is included. Windows queries primary-monitor-compatible modes; Web uses the current canvas.
- Latest Unity random and pressure-based probability formula are included.
- Unity target restored to Windows; Editor left open.
- No Play verification, standalone launch, browser gameplay test or itch.io publishing was performed.

[Windows report](Windows.md) | [WebGL report](WebGL.md) | [Upload guide](../../../Docs/BuildAndItch.md)
