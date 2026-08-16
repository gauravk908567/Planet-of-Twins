using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Attach to the PlayableDirector GO that runs the level-intro cinematic.
/// After the timeline stops, finds the AreaSpawnPoints component in any loaded
/// scene and teleports both twins to those positions.
///
/// WHY AreaSpawnPoints NOT TRANSFORMS: the Timeline can live in a different scene
/// than the gameplay start positions (e.g. Tutorial in Streets, starts in Park).
/// Cross-scene Transform refs don't serialize in Inspector. AreaSpawnPoints is
/// found via FindAnyObjectByType — no strings, no registry, no IDs.
///
/// SETUP:
///   1. On the PlayableDirector GO, add this component.
///   2. In the scene where gameplay begins (e.g. Park), add one GO with an
///      AreaSpawnPoints component and wire its leftStart / rightStart children.
///   3. Director Wrap Mode: "Hold" or "None" — stopped callback fires then.
/// </summary>
[RequireComponent(typeof(PlayableDirector))]
public class IntroTimelinePositioner : MonoBehaviour
{
    [Tooltip("Unfreeze + unlock movement on repositioning so players can move immediately.")]
    [SerializeField] private bool unfreezeOnPlace = true;

    private PlayableDirector _director;

    private void Awake() => _director = GetComponent<PlayableDirector>();

    private void OnEnable()
    {
        if (_director != null)
        {
            _director.played  += OnTimelinePlayed;
            _director.stopped += OnTimelineStopped;
        }
    }

    private void OnDisable()
    {
        if (_director != null)
        {
            _director.played  -= OnTimelinePlayed;
            _director.stopped -= OnTimelineStopped;
        }
    }

    // Lock both twins for the cutscene's duration. The old single-scene setup did this by
    // deactivating the Kai/Lyra GOs via Activation tracks; in multiscene we must NOT toggle the
    // Persistent twin GOs (R11/BUG-W15), so we lock their movement instead. OnTimelineStopped →
    // PlaceTwins() unlocks. This covers every entry path (Bootstrap, Intro, direct-area play),
    // unlike IntroController's lock which only runs in the full boot flow.
    private void OnTimelinePlayed(PlayableDirector _) => LockTwins(true);
    private void OnTimelineStopped(PlayableDirector _) => PlaceTwins();

    private static void LockTwins(bool locked)
    {
        var roster = PlayerRoster.Instance;
        if (roster == null) return;
        LockTwin(roster.TwinA, locked);
        LockTwin(roster.TwinB, locked);
    }

    private static void LockTwin(Player p, bool locked)
    {
        if (p == null) return;
        (p.Movement as IMovementFreezable)?.SetFrozen(locked);
        p.Movement?.SetMovementLocked(locked);
    }

    private void PlaceTwins()
    {
        // Prefer AreaSpawnPoints in the same scene as this director.
        // FindAnyObjectByType can find ones from adjacent streaming scenes (e.g. L2_Streets)
        // which would teleport twins to the wrong area.
        AreaSpawnPoints spawnPoints = null;
        foreach (var sp in FindObjectsByType<AreaSpawnPoints>(FindObjectsSortMode.None))
        {
            if (sp.gameObject.scene == gameObject.scene) { spawnPoints = sp; break; }
        }
        spawnPoints ??= FindAnyObjectByType<AreaSpawnPoints>();

        if (spawnPoints == null)
        {
            Debug.LogWarning("[IntroTimelinePositioner] No AreaSpawnPoints found in loaded scenes.", this);
            return;
        }

        // Identify left/right from PlayerRoster — deterministic, no positional guessing.
        Player left  = PlayerRoster.Instance?.TwinA;
        Player right = PlayerRoster.Instance?.TwinB;

        Teleport(left,  spawnPoints.leftStart);
        Teleport(right, spawnPoints.rightStart);

        if (unfreezeOnPlace)
        {
            UnfreezePlayer(left);
            UnfreezePlayer(right);
        }

        Debug.Log("[IntroTimelinePositioner] Twins repositioned to gameplay start.");
    }

    private static void Teleport(Player p, Transform target)
    {
        if (p == null || target == null) return;
        var cc = p.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        p.transform.position = target.position;
        p.transform.rotation = target.rotation;
        if (cc != null) cc.enabled = true;
    }

    private static void UnfreezePlayer(Player p)
    {
        if (p == null) return;
        (p.Movement as IMovementFreezable)?.SetFrozen(false);
        p.Movement?.SetMovementLocked(false);
    }
}
