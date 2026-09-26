using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace PoT.Diagnostics
{
    /// <summary>
    /// The last frame before a menu opened, for a bug report (game.md §27.3). Only the latest capture is kept, as
    /// raw pixels: a capture costs a GPU copy, a downscale and an async readback, and the JPG is encoded only when a
    /// report asks for it (<see cref="EncodeJpg"/>).
    ///
    /// Call <see cref="CaptureNow"/> from a coroutine right after <c>yield return new WaitForEndOfFrame()</c>, on
    /// the frame BEFORE the menu becomes visible, so the menu itself isn't in the picture. The screen-space HUD is.
    /// </summary>
    public static class ScreenshotCapture
    {
        public const int DefaultMaxWidth = 1280;
        public const int DefaultJpgQuality = 75;

        private static byte[] _pixels;   // RGBA32, Unity row order (bottom row first)
        private static int _width;
        private static int _height;
        private static int _generation;  // a newer capture wins over a readback still in flight
        private static bool _unsupportedLogged;

        public static bool HasCapture => _pixels != null && _width > 0 && _height > 0;

        /// <summary>When the kept capture was taken (UTC). Default when there is none.</summary>
        public static DateTime CapturedUtc { get; private set; }

        /// <summary>Copies the finished frame into a render texture scaled to at most <paramref name="maxWidth"/>
        /// pixels wide, and reads it back asynchronously. The previous capture stays until the new one arrives.</summary>
        public static void CaptureNow(int maxWidth = DefaultMaxWidth)
        {
            if (!SystemInfo.supportsAsyncGPUReadback)
            {
                if (!_unsupportedLogged)
                    Debug.LogWarning("[Diagnostics] This GPU can't read the screen back asynchronously; bug reports go " +
                                     "without a screenshot.");
                _unsupportedLogged = true;
                return;
            }
            int screenW = Screen.width, screenH = Screen.height;
            if (screenW <= 0 || screenH <= 0) return;

            float scale = Mathf.Min(1f, (float)maxWidth / screenW);
            int w = Mathf.Max(1, Mathf.RoundToInt(screenW * scale));
            int h = Mathf.Max(1, Mathf.RoundToInt(screenH * scale));

            var full = RenderTexture.GetTemporary(screenW, screenH, 0, RenderTextureFormat.ARGB32);
            var small = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            ScreenCapture.CaptureScreenshotIntoRenderTexture(full);
            // Where texture coordinates start at the top (D3D, Metal, Vulkan), the screen copy arrives upside down.
            if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(full, small, new Vector2(1f, -1f), new Vector2(0f, 1f));
            else Graphics.Blit(full, small);
            RenderTexture.ReleaseTemporary(full);

            int generation = ++_generation;
            AsyncGPUReadback.Request(small, 0, TextureFormat.RGBA32,
                request => OnReadback(request, small, generation));
        }

        /// <summary>The kept capture as a JPG, or null when there is none.</summary>
        public static byte[] EncodeJpg(int quality = DefaultJpgQuality)
        {
            if (!HasCapture) return null;
            return ImageConversion.EncodeArrayToJPG(_pixels, GraphicsFormat.R8G8B8A8_UNorm,
                                                    (uint)_width, (uint)_height, 0, quality);
        }

        /// <summary>Forgets the kept capture.</summary>
        public static void Clear()
        {
            _generation++;
            _pixels = null;
            _width = _height = 0;
            CapturedUtc = default;
        }

        internal static void ResetStatics()
        {
            Clear();
            _unsupportedLogged = false;
        }

        private static void OnReadback(AsyncGPUReadbackRequest request, RenderTexture source, int generation)
        {
            RenderTexture.ReleaseTemporary(source);
            if (request.hasError)
            {
                Debug.LogWarning("[Diagnostics] Screenshot readback failed; the report goes without a new screenshot.");
                return;
            }
            if (generation != _generation) return;   // a newer capture (or a Clear) came after this one

            var data = request.GetData<byte>();
            if (_pixels == null || _pixels.Length != data.Length) _pixels = new byte[data.Length];
            data.CopyTo(_pixels);
            _width = request.width;
            _height = request.height;
            CapturedUtc = DateTime.UtcNow;
        }
    }
}
