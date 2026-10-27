using System.Collections.Generic;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
#endif

namespace ColaCola.Presentation
{
    /// <summary>Actual monitor-compatible modes; never adds an unsupported authored preset or saved value.</summary>
    public static class ColaDisplayModes
    {
        public static List<Vector2Int> Primary()
        {
            var modes = new HashSet<Vector2Int>();
#if UNITY_WEBGL && !UNITY_EDITOR
            // A web page owns a canvas, not the desktop's monitor modes.
            modes.Add(new Vector2Int(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height)));
#else
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            AddWindowsPrimary(modes);
#endif
            if (modes.Count == 0)
            {
                var layout = new List<DisplayInfo>();
                try
                {
                    Screen.GetDisplayLayout(layout);
                    if (layout.Count > 0)
                    {
                        try { foreach (var mode in layout[0].resolutions) Add(modes, mode.width, mode.height); }
                        catch (System.NotSupportedException) { }
                        Add(modes, layout[0].width, layout[0].height);
                    }
                    else foreach (var mode in Screen.resolutions) Add(modes, mode.width, mode.height);
                }
                catch (System.NotSupportedException) { }
            }
#endif
            if (modes.Count == 0) Add(modes, Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            return new List<Vector2Int>(modes);
        }

        private static void Add(HashSet<Vector2Int> modes, int width, int height)
        { if (width > 0 && height > 0) modes.Add(new Vector2Int(width, height)); }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DisplayDevice
        {
            public int Size;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
        }

        // DEVMODEW has a fixed 220-byte ABI on both x86 and x64. Only these fields are needed.
        [StructLayout(LayoutKind.Explicit, Size = 220)]
        private struct DisplayMode
        {
            [FieldOffset(68)] public ushort Size;
            [FieldOffset(168)] public uint BitsPerPixel;
            [FieldOffset(172)] public uint Width;
            [FieldOffset(176)] public uint Height;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayDevicesW(string device, uint index, ref DisplayDevice result, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplaySettingsExW(string device, int index, ref DisplayMode result, uint flags);

        private static void AddWindowsPrimary(HashSet<Vector2Int> modes)
        {
            try
            {
                for (uint i = 0; i < 64; i++)
                {
                    var device = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
                    if (!EnumDisplayDevicesW(null, i, ref device, 0)) break;
                    if ((device.Flags & 4) == 0) continue; // DISPLAY_DEVICE_PRIMARY_DEVICE
                    var current = new DisplayMode { Size = (ushort)Marshal.SizeOf<DisplayMode>() };
                    if (EnumDisplaySettingsExW(device.Name, -1, ref current, 0))
                        Add(modes, (int)current.Width, (int)current.Height);
                    for (int index = 0; index < 8192; index++)
                    {
                        var mode = new DisplayMode { Size = (ushort)Marshal.SizeOf<DisplayMode>() };
                        // No EDS_RAWMODE or EDS_ROTATEDMODE: monitor-compatible/current-orientation modes only.
                        if (!EnumDisplaySettingsExW(device.Name, index, ref mode, 0)) break;
                        if (mode.BitsPerPixel == 32 && mode.Width <= int.MaxValue && mode.Height <= int.MaxValue)
                            Add(modes, (int)mode.Width, (int)mode.Height);
                    }
                    break;
                }
            }
            catch (System.DllNotFoundException) { }
            catch (System.EntryPointNotFoundException) { }
        }
#endif
    }
}
