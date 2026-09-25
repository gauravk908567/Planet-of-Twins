using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// P-C (controller nav) — the SELECTION highlight. Replaces the interim flat-gold ColorTint (which the user
/// flagged as looking cheap) with a soft <b>glowing outline</b> that follows the EventSystem's current
/// selection. One shared "follower" graphic (a 9-sliced glow frame, transparent centre) reparents onto
/// whatever is selected and frames it — so every screen gets an identical, premium-reading focus cue with no
/// per-button art and no scene wiring. Because it simply follows <see cref="EventSystem.current"/>'s
/// selection, it also lights the focused skill-tree node / modal button for free.
///
/// <para>Auto-attached to the active EventSystem by <see cref="UINavFocus.Focus"/> (like
/// <see cref="UINavFocusGuardian"/>) — FrontEnd and Persistent each own their EventSystem and never coexist
/// (GameBootstrapper unloads FrontEnd before Persistent loads), so the follower lives and dies with its
/// scene's EventSystem — no cross-scene reference (R2/R9).</para>
///
/// <para>Menus run under pause (<c>Time.timeScale = 0</c>), so the pulse animates on
/// <see cref="Time.unscaledTime"/> (R10 discipline). The glow is a runtime graphic only — never saved to any
/// scene. Interim look until final art lands; the tunables below are the dial-in knobs.</para>
/// </summary>
[DisallowMultipleComponent]
public class UINavHighlighter : MonoBehaviour
{
    // ── Tunables (interim; dial in during play-verify, same as the glyph sizes) ──────────────
    /// <summary>Gold accent of the glow (matches <see cref="UINavStyle.DefaultAccent"/>).</summary>
    private static readonly Color Accent = UINavStyle.DefaultAccent;
    /// <summary>How far the frame extends beyond the selected rect, in canvas units (glow bleed).</summary>
    private const float Padding = 8f;
    /// <summary>Sliced-border scale — LOWER = thicker glow. Border ≈ 24 / this px at scaleFactor 1.</summary>
    private const float BorderScale = 1.2f;
    private const float PulseSpeed = 3.2f;   // rad/s on unscaled time
    private const float AlphaMin = 0.5f;
    private const float AlphaMax = 1.0f;

    private static Sprite _glowSprite;       // shared across every highlighter instance

    private RectTransform _follower;
    private Image _image;
    private GameObject _current;             // the item the follower is currently framing

    private void Update()
    {
        var es = EventSystem.current;
        if (es == null) { HideIfShown(); return; }

        var sel = es.currentSelectedGameObject;

        // Only frame a live, interactable Selectable — a stale selection left over after a menu closed
        // (its button now inactive) reads as "nothing focused" and the glow hides (no gameplay glow).
        if (sel != null && sel.activeInHierarchy)
        {
            var s = sel.GetComponent<Selectable>();
            if (s == null || !s.IsInteractable()) sel = null;
        }
        else sel = null;

        if (sel == null) { HideIfShown(); return; }

        if (sel != _current) Attach(sel);
        Pulse();
    }

    private void Attach(GameObject target)
    {
        EnsureFollower();
        _current = target;

        var rt = _follower;
        rt.SetParent(target.transform, worldPositionStays: false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(-Padding, -Padding);   // extend past the button on all sides = glow bleed
        rt.offsetMax = new Vector2(Padding, Padding);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.SetAsLastSibling();                            // draw over the button bg; centre is unfilled so the
                                                         // label still shows and the glow only hugs the edges
        _follower.gameObject.SetActive(true);
    }

    private void Pulse()
    {
        if (_image == null) return;
        float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseSpeed);   // unscaled — menus pause the game
        var c = Accent;
        c.a = Mathf.Lerp(AlphaMin, AlphaMax, t);
        _image.color = c;
    }

    private void HideIfShown()
    {
        if (_current == null && (_follower == null || !_follower.gameObject.activeSelf)) return;
        _current = null;
        if (_follower != null)
        {
            // Park it back under this driver so it never lingers under a closing/destroyed menu subtree.
            _follower.SetParent(transform, worldPositionStays: false);
            _follower.gameObject.SetActive(false);
        }
    }

    private void EnsureFollower()
    {
        if (_follower != null) return;

        var go = new GameObject("UINavHighlight",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(transform, worldPositionStays: false);

        _follower = go.GetComponent<RectTransform>();
        _image = go.GetComponent<Image>();
        _image.sprite = GlowSprite;
        _image.type = Image.Type.Sliced;
        _image.fillCenter = false;                 // never draw the centre quad → label always visible, no overdraw
        _image.pixelsPerUnitMultiplier = BorderScale;
        _image.raycastTarget = false;              // never intercept clicks
        _image.maskable = false;                   // let the glow bleed past a RectMask2D (e.g. skill-tree viewport)
        _image.color = Accent;

        go.GetComponent<LayoutElement>().ignoreLayout = true;   // never rearranged by a LayoutGroup on the button
        go.SetActive(false);
    }

    /// <summary>Soft rounded glow frame, generated once. White (tinted by <see cref="Image.color"/>); a
    /// gaussian ring in the 9-slice border, transparent hole in the centre, rounded at the corners.</summary>
    private static Sprite GlowSprite
    {
        get
        {
            if (_glowSprite != null) return _glowSprite;

            const int N = 64;      // texture size
            const int B = 24;      // 9-slice border (px)
            const float P = B * 0.5f;    // ring centre — how deep the frame line sits from the outer edge
            const float W = B * 0.34f;   // ring falloff width

            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int dx = Mathf.Min(x, N - 1 - x);   // distance to nearest vertical edge
                int dy = Mathf.Min(y, N - 1 - y);   // distance to nearest horizontal edge

                float a;
                if (dx >= B && dy >= B)
                {
                    a = 0f;                          // interior hole (stretched away by 9-slice; unfilled anyway)
                }
                else
                {
                    // "Penetration" from the outer edge — 9-slice preserves border-pixel distance-from-edge,
                    // so this renders as a uniform-thickness frame; rounded in the corner block.
                    float pen;
                    if (dx < B && dy < B)
                    {
                        float rr = Mathf.Sqrt((B - dx) * (B - dx) + (B - dy) * (B - dy));
                        pen = B - rr;                // rounds the corner (outer diagonal → pen ≤ 0 → transparent)
                    }
                    else
                    {
                        pen = Mathf.Min(dx, dy);     // straight edge strip
                    }

                    if (pen <= 0f) a = 0f;
                    else { float t = (pen - P) / W; a = Mathf.Exp(-t * t); }   // gaussian ring
                }

                byte alpha = (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255);
                px[y * N + x] = new Color32(255, 255, 255, alpha);
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);

            _glowSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(B, B, B, B));
            _glowSprite.name = "UINavGlow";
            return _glowSprite;
        }
    }
}
