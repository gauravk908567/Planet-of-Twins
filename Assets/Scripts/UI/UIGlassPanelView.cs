using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives one PoT/UIGlassPanel Image. Generic backing-panel view — the ability panel is its
/// first consumer, not its definition. Any HUD slab wanting the frosted-glass look with the
/// travelling clan current uses this component.
///
/// Owns three things the shader cannot know by itself:
///   1. _PanelSize. UGUI does not hand a shader its rect, and without the true pixel size the
///      shader cannot compute its aspect, so the fixed-size caps and centre notch would stretch
///      with the panel. Pushed on every RectTransform dimension change.
///   2. Width growth. The panel widens as abilities unlock. Only the straight middle run grows
///      (the shader keeps caps and notch fixed in height units), so growth is a plain width
///      animation with no silhouette authoring.
///   3. Material instancing. Graphic.material does NOT auto-instance the way Renderer.material
///      does — assigning through it writes to the SHARED asset. Every panel would share one
///      width and the .mat file on disk would be dirtied. So the clone is explicit.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class UIGlassPanelView : MonoBehaviour
{
    private static readonly int PanelSizeID = Shader.PropertyToID("_PanelSize");
    private static readonly int CurrentColorAID = Shader.PropertyToID("_CurrentColorA");
    private static readonly int CurrentColorBID = Shader.PropertyToID("_CurrentColorB");
    private static readonly int CurrentStrengthID = Shader.PropertyToID("_CurrentStrength");
    private static readonly int CurrentSpeedID = Shader.PropertyToID("_CurrentSpeed");

    [Header("Target")]
    [Tooltip("The Image carrying the PoT/UIGlassPanel material. Defaults to this GameObject's Image.")]
    [SerializeField] private Image _panelImage;

    [Header("Width")]
    [Tooltip("ON  = this component owns the panel's width and grows it as abilities unlock.\n" +
             "OFF = it never touches the width; drag the RectTransform to whatever you like and " +
             "it stays. Turn this OFF if you want to hand-place and hand-size the panel.\n\n" +
             "Height, position and anchors are ALWAYS yours either way — this only ever writes " +
             "the width, and only when this is ON.")]
    [SerializeField] private bool _autoWidth = true;

    [Tooltip("Width with zero ability slots — the closed panel: two caps plus the centre notch.")]
    [SerializeField] private float _baseWidth = 260f;
    [Tooltip("Width added per unlocked ability slot.")]
    [SerializeField] private float _widthPerSlot = 92f;
    [Tooltip("Slots currently unlocked. Accord-state abilities REPLACE a base ability in place " +
             "and must never change this — otherwise the panel would resize mid-combat.")]
    [SerializeField, Min(0)] private int _slotCount;
    [Tooltip("Width units per second while growing. Zero snaps instantly.")]
    [SerializeField] private float _growSpeed = 420f;

    [Header("Current")]
    [SerializeField] private Color _currentColorLeft = new Color(1.0f, 0.78f, 0.29f, 1f);
    [SerializeField] private Color _currentColorRight = new Color(0.49f, 0.30f, 1.0f, 1f);
    [SerializeField, Range(0f, 4f)] private float _currentStrength = 1f;
    [SerializeField, Range(0f, 4f)] private float _currentSpeed = 0.35f;

    private RectTransform _rect;
    private Material _material;
    private Material _sourceMaterial;

    // Target width the panel is animating toward. Width itself lives on the RectTransform.
    private float _targetWidth;

    public int SlotCount => _slotCount;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        if (_panelImage == null) _panelImage = GetComponent<Image>();

        if (_panelImage == null)
        {
            Debug.LogError($"{nameof(UIGlassPanelView)} on '{name}' has no Image to drive. Disabling.", this);
            enabled = false;
            return;
        }

        EnsureMaterial();
        _targetWidth = ComputeWidth(_slotCount);
        if (_autoWidth) ApplyWidthImmediate(_targetWidth);
        else PushPanelSize();   // still tell the shader its size, just do not change it
    }

    private void OnEnable()
    {
        if (_rect == null) _rect = (RectTransform)transform;
        EnsureMaterial();
        PushAll();
    }

    /// <summary>
    /// Clone the shared material once so per-panel width and colours stay per-panel.
    /// The clone is HideAndDontSave so it never serializes into the scene — a scene-embedded
    /// clone is what previously made Project-window edits to the shared .mat appear to do
    /// nothing in the editor.
    /// </summary>
    private void EnsureMaterial()
    {
        if (_panelImage == null) return;
        if (_material != null) return;

        // NEVER touch Image.material outside play mode.
        //
        // This class is [ExecuteAlways], so Awake/OnValidate also run in the editor. Cloning there
        // and assigning the clone back to the Image DESTROYS the panel's material link: the clone
        // is HideAndDontSave, HideAndDontSave objects are not serialized, so the moment the scene
        // is saved and reloaded the Image's material reference is dead and UGUI silently falls
        // back to "Default UI Material" — a plain white quad. The authored M_UIGlassPanel is gone
        // and nothing logs a word about it. This is precisely what turned the panel into a solid
        // white bar, and it survives a scene save, so it is permanent damage rather than a glitch.
        //
        // UIBarView already guards its Awake the same way ("never touch Image.material here").
        // In edit mode the panel simply renders with the shared material's authored defaults,
        // which is correct WYSIWYG for everything except _PanelSize — that keeps the material's
        // default until play begins, so the caps may look slightly off-aspect in the Scene view.
        if (!Application.isPlaying) return;

        Material source = _panelImage.material;
        if (source == null) return;

        // Already ours (survived a domain reload with the reference intact).
        if (source.hideFlags == HideFlags.HideAndDontSave)
        {
            _material = source;
            return;
        }

        _sourceMaterial = source;
        _material = new Material(source)
        {
            name = source.name + " (UIGlassPanelView instance)",
            hideFlags = HideFlags.HideAndDontSave
        };
        _panelImage.material = _material;
    }

    private float ComputeWidth(int slots) => _baseWidth + _widthPerSlot * Mathf.Max(0, slots);

    /// <summary>
    /// Set how many ability slots the panel must span. Call this on unlock only.
    /// Accord-state abilities swap a base ability IN PLACE and must not call this.
    /// </summary>
    public void SetSlotCount(int slots, bool instant = false)
    {
        _slotCount = Mathf.Max(0, slots);
        _targetWidth = ComputeWidth(_slotCount);
        if (instant || _growSpeed <= 0f) ApplyWidthImmediate(_targetWidth);
    }

    private void ApplyWidthImmediate(float width)
    {
        if (_rect == null) return;
        _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        PushPanelSize();
    }

    private void Update()
    {
        if (_rect == null || !_autoWidth) return;

        // Unscaled: the HUD must keep animating during pause and during Setsuna (timeScale 0.15).
        float current = _rect.rect.width;
        if (!Mathf.Approximately(current, _targetWidth) && _growSpeed > 0f)
        {
            float next = Mathf.MoveTowards(current, _targetWidth, _growSpeed * Time.unscaledDeltaTime);
            _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, next);
            PushPanelSize();
        }
    }

    /// <summary>
    /// UGUI calls this whenever the rect resizes for any reason — layout groups, anchors, our
    /// own growth animation, or a designer dragging the handles in the Scene view. It is the one
    /// hook that catches every path, so _PanelSize is pushed from here rather than from Update.
    /// </summary>
    private void OnRectTransformDimensionsChange()
    {
        PushPanelSize();
    }

    private void PushPanelSize()
    {
        if (_material == null || _rect == null) return;
        Rect r = _rect.rect;
        _material.SetVector(PanelSizeID, new Vector4(r.width, r.height, 0f, 0f));
    }

    private void PushAll()
    {
        if (_material == null) return;
        _material.SetColor(CurrentColorAID, _currentColorLeft);
        _material.SetColor(CurrentColorBID, _currentColorRight);
        _material.SetFloat(CurrentStrengthID, _currentStrength);
        _material.SetFloat(CurrentSpeedID, _currentSpeed);
        PushPanelSize();
    }

    public void SetCurrentColors(Color left, Color right)
    {
        _currentColorLeft = left;
        _currentColorRight = right;
        if (_material == null) return;
        _material.SetColor(CurrentColorAID, left);
        _material.SetColor(CurrentColorBID, right);
    }

    /// <summary>
    /// Current strength doubles as the bond signal: run it low or stall the flow when the twins
    /// are far apart, so the panel's own light fails at the same moment the emblem cracks.
    /// </summary>
    public void SetCurrent(float strength, float speed)
    {
        _currentStrength = strength;
        _currentSpeed = speed;
        if (_material == null) return;
        _material.SetFloat(CurrentStrengthID, strength);
        _material.SetFloat(CurrentSpeedID, speed);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_panelImage == null) _panelImage = GetComponent<Image>();
        _rect = (RectTransform)transform;
        EnsureMaterial();   // no-op outside play mode — see the comment there

        // Re-pull the shared asset so Project-window edits to the .mat propagate into the live
        // clone, then re-apply the per-instance overrides on top. Play mode only: outside it
        // there is no clone, and the Image is pointed straight at the shared asset.
        if (_material != null && _sourceMaterial != null)
            _material.CopyPropertiesFromMaterial(_sourceMaterial);

        _targetWidth = ComputeWidth(_slotCount);

        // Only ever push width in the editor when this component OWNS it. Without this guard,
        // hand-resizing the panel in the Scene view snaps straight back and the designer cannot
        // place the HUD at all.
        if (!Application.isPlaying && _autoWidth) ApplyWidthImmediate(_targetWidth);
        PushAll();
    }
#endif

    private void OnDestroy()
    {
        if (_material == null) return;
        if (Application.isPlaying) Destroy(_material);
        else DestroyImmediate(_material);
        _material = null;
    }
}
