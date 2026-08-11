using UnityEngine;

public class TeleportMarkerPreview : MonoBehaviour
{
    [SerializeField] private GameObject markerObject;
    [SerializeField] private MarkerPlacementSettings settings;
    [SerializeField] private LayerMask obstacleLayer;
    //[SerializeField] private AudioSource warningAudio;
    //[SerializeField] private GameObject warningTextObject; // "Can't place here" UI

    // Material slots for marker colour feedback
    [SerializeField] private Renderer markerRenderer;
    [SerializeField] private Material clearMaterial;    // green � clear position
    [SerializeField] private Material obstacleMaterial; // red � inside obstacle

    private TeleportAbility _teleportAbility;
    private Transform _casterTransform;
    private Transform _targetTransform;

    private Vector3 _currentMarkerPos;
    private bool _markerInObstacle;

    // The aim visual is now the tele_castmark CUE following the (renderer-hidden) marker object --
    // the old green/red disc materials are retired (user call 2026-07-09). Marker LOGIC unchanged.
    private CueHandle _previewHandle;

    public void Initialise(TeleportAbility ability, Transform caster, Transform target)
    {
        _teleportAbility = ability;
        _casterTransform = caster;
        _targetTransform = target;
    }

    private void Awake()
    {
        markerObject.SetActive(false);
        // Retire the coloured disc: the marker object keeps doing placement math, invisibly.
        foreach (var r in markerObject.GetComponentsInChildren<Renderer>(true))
            r.enabled = false;
       // warningTextObject?.SetActive(false);
    }

    public void Show(Transform otherTwinPos)
    {
        // No aiming while the soul is already out — spam-showing the preview mid-gate leaked a
        // second held castmark that nothing ever stopped (playtest round 3).
        if (_teleportAbility != null && _teleportAbility.IsActive) return;

        // Idempotent while live: the dispatcher calls Show every held frame — restarting the held
        // cue each frame cleared its particles every frame, so the marker never rendered during the
        // hold (BUG-064). A live preview just keeps riding the marker; Update() tracks position.
        if (markerObject.activeSelf && (FxManager.Instance?.IsPlaying(_previewHandle) ?? false))
            return;

        // Re-entrancy safe: a second Show without a Hide must not orphan the first held cue.
        FxManager.Instance?.Stop(_previewHandle);
        _previewHandle = CueHandle.None;

        markerObject.SetActive(true);
        UpdateMarkerPosition();

        // Held castmark cue rides the marker while aiming (fail-safe: no book = logic-only aiming).
        var book = VfxLibraryProvider.Instance?.Player?.Teleport;
        if (book != null)
            _previewHandle = FxManager.Instance?.PlayBook(book, FxIds.Player.Teleport.tele_castmark,
                CueContext.Follow(markerObject.transform)) ?? CueHandle.None;
    }

    private void Update()
    {
        if (markerObject.activeSelf)
            UpdateMarkerPosition();
    }

    // A held preview cue must never outlive this component (scene unload / player despawn).
    private void OnDisable()
    {
        FxManager.Instance?.Stop(_previewHandle);
        _previewHandle = CueHandle.None;
    }

    public void Hide()
    {
        FxManager.Instance?.Stop(_previewHandle);
        _previewHandle = CueHandle.None;
        markerObject.SetActive(false);
        //warningTextObject?.SetActive(false);
    }

    private void UpdateMarkerPosition()
    {
        if (_casterTransform == null || _targetTransform == null) return;

        Vector3 casterPos = _casterTransform.position;
        Vector3 targetPos = _targetTransform.position;

        // Barrier midpoint between the two twins
        Vector3 barrierMidpoint = (casterPos + targetPos) * 0.5f;

        // FIX 1: Direction from barrier toward TARGET (opposite side from caster)
        // Previously was (casterPos - barrierMidpoint) � wrong direction
        Vector3 dirToTarget = (targetPos - barrierMidpoint).normalized;

        float maxDist = settings != null ? settings.MaxDistanceFromBarrier : 8f;
        float minDist = settings != null ? settings.MinDistanceFromBarrier : 4f;
        int steps = settings != null ? settings.FallbackSteps : 8;
        float checkR = settings != null ? settings.ObstacleCheckRadius : 0.4f;

        Vector3 bestPosition = barrierMidpoint + dirToTarget * maxDist;
        bool foundClearSpot = false;

        // Try from max down to min � pick FARTHEST clear position
        for (int i = steps; i >= 0; i--)
        {
            float t = (float)i / steps;
            float dist = Mathf.Lerp(minDist, maxDist, t);
            Vector3 candidate = barrierMidpoint + dirToTarget * dist;
            candidate.y = casterPos.y; // stay on ground plane

            bool obstructed = Physics.CheckSphere(candidate, checkR, obstacleLayer);

            if (!obstructed)
            {
                bestPosition = candidate;
                foundClearSpot = true;
                break; // farthest valid found � stop
            }
        }

        _currentMarkerPos = bestPosition;
        _markerInObstacle = !foundClearSpot;

        markerObject.transform.position = bestPosition;

        // FIX 2 (phase-through): always allow teleport, just show warning
        if (_markerInObstacle)
        {
            // Red marker � soul will phase through
            if (markerRenderer != null && obstacleMaterial != null)
                markerRenderer.sharedMaterial = obstacleMaterial;

           // warningTextObject?.SetActive(true);

            // Play warning sound once when first entering obstacle
           // if (warningAudio != null && !warningAudio.isPlaying)
           //     warningAudio.Play();
        }
        else
        {
            // Green marker � clear position
            if (markerRenderer != null && clearMaterial != null)
                markerRenderer.sharedMaterial = clearMaterial;

           // warningTextObject?.SetActive(false);
        }

        // Always notify TeleportAbility of marker position
        // Soul will travel here regardless of obstacles (phase-through)
        _teleportAbility?.SetMarkerPosition(bestPosition);
    }

    private void OnDrawGizmos()
    {
        if (!markerObject.activeSelf) return;
        Gizmos.color = _markerInObstacle ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(_currentMarkerPos,
            settings != null ? settings.ObstacleCheckRadius : 0.4f);
    }
}