using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.AI.Navigation;
using CommonCore;

/// <summary>
/// GameDebugger v2 (P12). The TestLab bench: spawn any enemy through the REAL pooled
/// path, then damage / kill / stun / possess it, set its mood (watch Manpu + the aura), force its
/// archetype behaviours, toggle perception, fire any cue by book+id, grant skill points, teleport the twins.
///
/// Residency: the TestLab scene (area-resident; works in any scene). Persistent deps resolve per R4 in
/// Start (Instance properties); TimeFactorBootstrapper is a non-singleton Persistent component → the
/// allowed scene-scoped sweep (R4 note, EnemyFreezeService precedent).
///
/// SHIP SAFETY: every entry point checks <see cref="DevConfig.Trainer"/> (master fail-safe + release-build
/// hard-off — a release build can never show this panel regardless of the asset). TestLab is never in
/// Build Settings. Toggle: Ctrl+` combo (serialized, rebindable — bare keys collide with other tools).
///
/// Spawning replicates EnemySpawner.SpawnEnemy exactly (the real lifecycle, so pooled-reuse bugs reproduce):
/// pool.Get → position → NavMeshAgent.Warp+enable → SetPoolProvider (fires on_enemyspawn + reveal-delay) →
/// ITimeAffected register → ApplyData → deathNotifier.Register. No zone/tracker (TestLab has no SpawnZone —
/// ZoneEnemyTracker.HomeZone stays null; every consumer is null-safe on it).
/// </summary>
public class GameDebuggerV2 : MonoBehaviour
{
    [System.Serializable]
    public class SpawnEntry
    {
        public string label;
        public GameObject prefab;
        [Tooltip("Optional EnemyData override — null keeps the prefab's own serialized data (ApplyData is skipped).")]
        public EnemyData data;
    }

    [Header("Spawnables (Auto-Fill via context menu, editor-only)")]
    [SerializeField] private List<SpawnEntry> _spawnables = new List<SpawnEntry>();

    [Header("Cue books (Auto-Fill via context menu, editor-only)")]
    [SerializeField] private List<CueBookData> _cueBooks = new List<CueBookData>();

    [Header("POI bench prefabs (optional — empty slots spawn greybox primitives with real components)")]
    [SerializeField] private GameObject _spawnPointPoiPrefab;
    [SerializeField] private GameObject _ritualPoiPrefab;
    [SerializeField] private GameObject _barrierPoiPrefab;

    [Header("Scene anchors (TestLab-local, R1)")]
    [SerializeField] private Transform _enemyPad;
    [SerializeField] private Transform _twinPad;
    [Tooltip("TestLab's ground surface — baked at runtime in Start so the scene needs no manual bake.")]
    [SerializeField] private NavMeshSurface _navMeshSurface;

    [Header("Panel toggle (COMBO — single keys collide with other tools; F9 was the profiler)")]
    [Tooltip("Held modifier for the toggle combo. Rebind here if it fights another tool.")]
    [SerializeField] private KeyCode _toggleModifier = KeyCode.LeftControl;
    [Tooltip("Key pressed while the modifier is held. Default: Ctrl+` (backquote — console-style).")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.BackQuote;

    [Tooltip("Snap both twins onto the TwinPad on Start. TestLab is not a streamed area, so the twins wake " +
             "at their Persistent-authored position (meant for a level) — off this plane they fall into the " +
             "void before anything runs. ON for TestLab.")]
    [SerializeField] private bool _snapTwinsOnStart = true;

    // ── Persistent deps (R4 — resolved in Start, fail-loud) ──────────────────
    private EnemyPool _pool;
    private SkillTreeManager _skillTree;
    private TwinSelector _twins;
    private EnemyDeathNotifier _deathNotifier;
    private FxManager _fx;
    private TimeFactorBootstrapper _timeFactor;   // non-singleton Persistent component (allowed sweep)

    // ── Panel state ───────────────────────────────────────────────────────────
    private bool _visible;
    private Vector2 _scroll;
    private Rect _winRect = new Rect(10f, 10f, 800f, 800f);
    private Vector2 _winSize = new Vector2(800f, 800f);      // user-resizable: corner grip OR the W/H fields
    private bool _resizing;                                   // corner-grip drag in progress
    private string _winWText, _winHText;                      // typed-resolution fields (footer)
    private const int WindowId = 0xDB62;                     // arbitrary, just unique among IMGUI windows
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private GameObject _selected;
    private int _bookIndex;
    private bool _twinsDetectable = true;
    private bool _godMode;                                    // twins take no damage (SetInvincible both)
    private bool _twinsHaveWeapon;                            // melee bench: SetHasWeapon on both twins (TestLab has no SwordPickup)
    private readonly HashSet<GameObject> _dummies = new HashSet<GameObject>();   // dummy-mode enemies
    private float _gradeProgress;          // story-grading bench slider (mirrors StoryGradeDirector)
    private string[] _gradeIds;            // cached — GradeIds() allocates, OnGUI runs every frame
    private string[] _skyIds;              // cached SkyStateDriver ids for the World & Sky bench

    // Grayscale WORLD test (checklist item 21 — ArtStyle "grayscale-survives"): a dev-owned global
    // desaturation volume driven by the slider below. Created lazily, destroyed in OnDestroy (its
    // profile is a runtime instance) so no asset is dirtied and nothing ships — the whole panel is
    // DevConfig.Trainer-gated + release-stripped, so this is the ONLY access to it.
    private Volume _grayscaleVolume;
    private ColorAdjustments _grayscaleCA;
    private float _grayscaleAmount;        // 0 = full colour, 1 = full grayscale (saturation 0 → -100)

    private void Awake()
    {
        // R8 — Awake wires SELF (children by name), so the TestLab scene needs zero Inspector wiring.
        if (_enemyPad == null)       _enemyPad       = transform.Find("EnemyPad");
        if (_twinPad == null)        _twinPad        = transform.Find("TwinPad");
        if (_navMeshSurface == null) _navMeshSurface = GetComponentInChildren<NavMeshSurface>(true);
    }

    private void Start()
    {
        if (!DevConfig.Trainer) { enabled = false; return; }   // ship safety — panel never runs outside dev

#if UNITY_EDITOR
        // Editor convenience: first run auto-fills the lists so the bench is zero-config in the editor.
        // (A development BUILD needs the lists serialized — run the context menus + save the scene for that.)
        if (_spawnables.Count == 0) AutoFillSpawnables();
        if (_cueBooks.Count == 0)   AutoFillCueBooks();
#endif

        _pool          = EnemyPool.Instance;
        _skillTree     = SkillTreeManager.Instance;
        _twins         = TwinSelector.Instance;
        _deathNotifier = EnemyDeathNotifier.Instance;
        _fx            = FxManager.Instance;
        _timeFactor    = FindAnyObjectByType<TimeFactorBootstrapper>();

        if (_pool == null)
            Debug.LogError("[GameDebuggerV2] EnemyPool.Instance unresolved — is Persistent loaded? " +
                           "(direct-play needs PersistentSceneAutoLoader)", this);

        // TestLab self-sufficiency: bake the flat lab surface at runtime (a plane bakes in milliseconds),
        // so spawned NavMeshAgents have a mesh without any manual editor bake step.
        if (_navMeshSurface != null && _navMeshSurface.navMeshData == null)
            _navMeshSurface.BuildNavMesh();

        // The twins wake at their Persistent-authored position (a level position, not this plane) and
        // gravity drops them into the void before any button can be pressed — snap them onto the pad now.
        if (_snapTwinsOnStart) TeleportTwinsToPad();
    }

    private void Update()
    {
        // Raw Input is banned outside TwinInputReader for GAMEPLAY (it must respect the tutorial gate);
        // this dev-only panel toggle deliberately bypasses the gate (same class as the L/O/P/I/K trainer
        // keys) and is DevConfig.Trainer-gated + release-stripped, so it can never reach players.
        // COMBO toggle (modifier held + key) — a bare key collides with other tools (F9 was the profiler).
        if (DevConfig.Trainer && Input.GetKey(_toggleModifier) && Input.GetKeyDown(_toggleKey))
            _visible = !_visible;

        // Ctrl+1..9 = spawn the nth spawnable at the enemy pad (user request 2026-07-08 — always a
        // COMBO, never bare F-keys: F3/F9 etc. collide with Unity editor functions like the profiler).
        if (DevConfig.Trainer && Input.GetKey(_toggleModifier))
        {
            for (int k = 0; k < 9 && k < _spawnables.Count; k++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + k))
                    Spawn(_spawnables[k]);
            }
        }
    }

    private void TeleportTwinsToPad()
    {
        Vector3 pad = _twinPad != null ? _twinPad.position : transform.position;
        // +1 m up so the CharacterController never re-enables intersecting the ground (fall-through).
        TeleportTwin(_twins?.LeftTwin,  pad + Vector3.left  + Vector3.up);
        TeleportTwin(_twins?.RightTwin, pad + Vector3.right + Vector3.up);
    }

    // ── Spawn (the real pooled path — mirrors EnemySpawner.SpawnEnemy) ────────
    private void Spawn(SpawnEntry entry)
    {
        if (_pool == null || entry?.prefab == null) return;

        Vector3 pos = (_enemyPad != null ? _enemyPad.position : transform.position)
                      + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));

        var instance = _pool.Get(entry.prefab);
        if (instance == null) return;

        instance.transform.position = pos;
        instance.transform.rotation = Quaternion.identity;

        var agent = instance.GetComponent<NavMeshAgent>();
        if (agent != null) { agent.Warp(pos); agent.enabled = true; }

        var enemy = instance.GetComponent<Enemy>();
        if (enemy == null) { _pool.Return(entry.prefab, instance); return; }

        enemy.SetPoolProvider(_pool, entry.prefab);                       // fires on_enemyspawn + reveal-delay
        if (enemy is ITimeAffected ta) _timeFactor?.RegisterEntity(ta);   // Setsuna/freeze participation
        if (entry.data != null) enemy.ApplyData(entry.data);

        instance.GetComponent<ZoneEnemyTracker>()?.ResetForPool();        // HomeZone stays null (no zone here)
        _deathNotifier?.Register(enemy.Health);                           // kill cues + counters stay real
        if (_dummies.Contains(instance)) SetDummy(instance, enemy, false);   // pool reuse never inherits dummy mode

        _spawned.Add(instance);
        _selected = instance;
    }

    // ── IMGUI panel ────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        if (!DevConfig.Trainer) return;
        string combo = $"{_toggleModifier}+{_toggleKey}";
        if (!_visible)
        {
            GUI.Label(new Rect(10, 10, 340, 22), $"[{combo}] GameDebugger v2");
            return;
        }

        // Resizable + draggable window (user request 2026-07-10: full-height fixed panel was the pain).
        // Size comes from the corner grip or the typed W/H fields; clamped to the game view every frame.
        _winSize.x = Mathf.Clamp(_winSize.x, 300f, Screen.width);
        _winSize.y = Mathf.Clamp(_winSize.y, 240f, Screen.height);
        _winRect.width  = Mathf.Min(_winSize.x, Screen.width - 10f);
        _winRect.height = Mathf.Min(_winSize.y, Screen.height - 10f);
        _winRect.x      = Mathf.Clamp(_winRect.x, 0f, Mathf.Max(0f, Screen.width  - _winRect.width));
        _winRect.y      = Mathf.Clamp(_winRect.y, 0f, Mathf.Max(0f, Screen.height - _winRect.height));
        _winRect = GUI.Window(WindowId, _winRect, DrawWindow, $"GameDebugger v2 — TestLab bench  [{combo} hide]");
    }

    private void DrawWindow(int id)
    {
        // FIXED top block (never scrolls): spawn buttons + the global kill/FX/twin controls.
        DrawSpawnSection();
        DrawGlobalSection();

        // Everything else scrolls: the spawned-enemy list opens a per-enemy action menu on click.
        _scroll = GUILayout.BeginScrollView(_scroll);
        DrawSelectedSection();
        DrawPoiSection();
        DrawPerceptionSection();
        DrawCueSection();
        DrawGradingSection();
        DrawWorldSkySection();
        DrawGrayscaleSection();
        DrawMetaSection();
        GUILayout.EndScrollView();

        DrawResizeFooter();
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));   // title bar drags the panel anywhere on screen
    }

    // Footer: typed W/H resolution fields + a corner drag-grip — both resize the panel live.
    private void DrawResizeFooter()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"size {(int)_winSize.x}×{(int)_winSize.y}", GUILayout.Width(90));
        GUILayout.Label("W", GUILayout.Width(15));
        _winWText = GUILayout.TextField(_winWText ?? ((int)_winSize.x).ToString(), GUILayout.Width(48));
        GUILayout.Label("H", GUILayout.Width(15));
        _winHText = GUILayout.TextField(_winHText ?? ((int)_winSize.y).ToString(), GUILayout.Width(48));
        if (GUILayout.Button("Apply", GUILayout.Width(50)))
        {
            if (int.TryParse(_winWText, out int w)) _winSize.x = w;
            if (int.TryParse(_winHText, out int h)) _winSize.y = h;
            _winWText = _winHText = null;   // re-sync the fields to the clamped size next frame
        }
        GUILayout.FlexibleSpace();
        GUILayout.Label("◢ drag", GUILayout.Width(50));
        GUILayout.EndHorizontal();

        // Corner grip: window-local coords (Event.current inside a GUI.Window callback is window-local).
        var grip = new Rect(_winRect.width - 26f, _winRect.height - 26f, 26f, 26f);
        var ev = Event.current;
        if (ev.type == EventType.MouseDown && grip.Contains(ev.mousePosition)) { _resizing = true;  ev.Use(); }
        else if (_resizing && ev.type == EventType.MouseDrag)                  { _winSize += ev.delta; ev.Use(); }
        else if (_resizing && (ev.type == EventType.MouseUp || ev.rawType == EventType.MouseUp))
                                                                               { _resizing = false; ev.Use(); }
    }

    // Global controls that must never scroll away (user layout spec 2026-07-10).
    private void DrawGlobalSection()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Kill ALL"))
            foreach (var g in _spawned)
                if (g != null && g.activeInHierarchy)
                    g.GetComponent<Enemy>()?.Health.TakeDamage(new DamageData(99999f, DamageType.Combat));
        if (GUILayout.Button("STOP ALL FX")) _fx?.StopAll();
        if (GUILayout.Button("Twins→pad")) TeleportTwinsToPad();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        // God mode — observe grabs/binds/drains without melee interrupting (PlayerHealthComponent.SetInvincible).
        bool god = GUILayout.Toggle(_godMode, " God mode (twins take no damage)");
        if (god != _godMode)
        {
            _godMode = god;
            _twins?.LeftTwin?.GetComponent<PlayerHealthComponent>()?.SetInvincible(god);
            _twins?.RightTwin?.GetComponent<PlayerHealthComponent>()?.SetInvincible(god);
        }
        // Rescue/soul bench: down one twin through the REAL damage path → rescue starts, the soul spawns,
        // Weaver's Gate + the Siphon ghost-bind become testable. (God mode wins if both are on.)
        if (GUILayout.Button("Down L twin", GUILayout.Width(90)))
            _twins?.LeftTwin?.GetComponent<PlayerHealthComponent>()
                  ?.TakeDamage(new DamageData(99999f, DamageType.Combat));
        if (GUILayout.Button("Down R twin", GUILayout.Width(90)))
            _twins?.RightTwin?.GetComponent<PlayerHealthComponent>()
                  ?.TakeDamage(new DamageData(99999f, DamageType.Combat));
        // Raw soul activation (playtest round 2 — SiphonGhost has no target without a soul out).
        // The REAL path is Down-a-twin (full rescue flow); this just wakes the Persistent SoulPlayer
        // at the pad for isolated ghost/bind benching.
        if (GUILayout.Button("Soul→pad", GUILayout.Width(80))) ActivateSoulAtPad();
        GUILayout.EndHorizontal();

        // Melee bench (user request 2026-07-13): TestLab has no SwordPickup, so the twins wake WEAPONLESS —
        // PlayerAttackController.PerformAttack no-ops on !_hasWeapon, so E does nothing and the per-twin slash
        // cues never fire. The toggle grants the weapon exactly like SwordPickup / SoftResetController do
        // (SetHasWeapon → the twin activates its own sword GO, R1), so the REAL E-input path swings both twins.
        // The buttons drive PerformAttack directly for single-twin slash testing (each controller's own isKai
        // picks electro=Kai / stone=Lyra); they auto-grant the weapon so one click always swings.
        GUILayout.BeginHorizontal();
        bool weapon = GUILayout.Toggle(_twinsHaveWeapon, " Melee weapon (E swings + slashes)");
        if (weapon != _twinsHaveWeapon) { _twinsHaveWeapon = weapon; SetTwinsWeapon(weapon); }
        if (GUILayout.Button("Slash L (Lyra)", GUILayout.Width(110))) Slash(_twins?.LeftTwin);
        if (GUILayout.Button("Slash R (Kai)",  GUILayout.Width(110))) Slash(_twins?.RightTwin);
        if (GUILayout.Button("Slash both",     GUILayout.Width(90)))  { Slash(_twins?.LeftTwin); Slash(_twins?.RightTwin); }
        GUILayout.EndHorizontal();
    }

    // Melee bench: grant/revoke the sword on both twins exactly as SwordPickup does (R1: the twin owns its
    // own weapon GO). GetComponentInChildren mirrors SoftResetController — the controller can sit on a child.
    private void SetTwinsWeapon(bool on)
    {
        _twins?.LeftTwin?.GetComponentInChildren<PlayerAttackController>(true)?.SetHasWeapon(on);
        _twins?.RightTwin?.GetComponentInChildren<PlayerAttackController>(true)?.SetHasWeapon(on);
    }

    // Fire one twin's melee through the REAL path (PerformAttack → attack anim → OnAttackHitFrame event →
    // ExecuteHitDetection → MeleeAttackStrategy → slash + hit cues). Auto-grants the weapon so a click always swings.
    private void Slash(Player twin)
    {
        var atk = twin?.GetComponentInChildren<PlayerAttackController>(true);
        if (atk == null) return;
        if (!atk.HasWeapon) { atk.SetHasWeapon(true); _twinsHaveWeapon = true; }
        atk.PerformAttack();
    }

    private void ActivateSoulAtPad()
    {
        var soul = FindAnyObjectByType<SoulPlayer>(FindObjectsInactive.Include);
        if (soul == null) { Debug.LogWarning("[GameDebuggerV2] No SoulPlayer found (Persistent loaded?)"); return; }
        Vector3 pad = (_twinPad != null ? _twinPad.position : transform.position) + Vector3.up * 1.5f;
        soul.gameObject.SetActive(true);
        var cc = soul.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        soul.transform.position = pad;
        if (cc != null) cc.enabled = true;
        soul.ShouldSoulSleep(false);
    }

    private void DrawSpawnSection()
    {
        GUILayout.Label("── Spawn (real pooled path) — Ctrl+1..9 hotkeys ──");
        int perRow = 3, i = 0;
        while (i < _spawnables.Count)
        {
            GUILayout.BeginHorizontal();
            for (int c = 0; c < perRow && i < _spawnables.Count; c++, i++)
            {
                var e = _spawnables[i];
                string label = string.IsNullOrEmpty(e.label) ? (e.prefab ? e.prefab.name : "—") : e.label;
                if (i < 9) label = (i + 1) + ". " + label;   // Ctrl+<n> spawns this entry
                if (GUILayout.Button(label)) Spawn(e);
            }
            GUILayout.EndHorizontal();
        }
        if (_spawnables.Count == 0)
            GUILayout.Label("(no spawnables — use the component context menu: Auto-Fill Spawnables)");
    }

    private void DrawSelectedSection()
    {
        _spawned.RemoveAll(g => g == null || !g.activeInHierarchy);   // pool-returned / destroyed purge
        _dummies.RemoveWhere(g => g == null || !g.activeInHierarchy);
        if (_selected != null && !_selected.activeInHierarchy) _selected = null;

        GUILayout.Space(6);
        GUILayout.Label($"── Active spawned: {_spawned.Count} ──");
        foreach (var g in _spawned)
        {
            bool isSel = g == _selected;
            if (GUILayout.Toggle(isSel, $" {g.name}  (HP {ReadHp(g)})") && !isSel) _selected = g;
        }

        if (_selected == null) { GUILayout.Label("(select an enemy for combat / mood / force buttons)"); return; }
        var enemy = _selected.GetComponent<Enemy>();
        if (enemy == null) return;

        GUILayout.Space(4);
        GUILayout.Label($"── Selected: {_selected.name} ──");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Damage 10"))
            enemy.Health.TakeDamage(new DamageData(10f, DamageType.Combat));
        if (GUILayout.Button("Kill"))
            enemy.Health.TakeDamage(new DamageData(99999f, DamageType.Combat));
        if (GUILayout.Button("Stun 2s")) enemy.ApplyStun(2f);
        if (GUILayout.Button("Possess 5s") && enemy is IPossessable p) p.ApplyPossession(5f, 1.5f);
        GUILayout.EndHorizontal();

        // Dummy mode — stands still, swings nothing, but the brain/mood/Manpu stack keeps reacting
        // (speed 0 + attacks off; restored from EnemyData on untoggle / respawn).
        bool isDummy = _dummies.Contains(_selected);
        bool dummyNow = GUILayout.Toggle(isDummy, " Dummy (no move / no attack — reactions live)");
        if (dummyNow != isDummy) SetDummy(_selected, enemy, dummyNow);

        // Dark-energy bench (user request 2026-07-10): set the level absolutely (thresholds fire like a
        // real gain), or freeze gain so ONE enemy of a pair can be escalated in isolation.
        // InChildren: some prefabs carry EnemyDarkEnergy on a child, and a root-only GetComponent hid the
        // whole slider (playtest round 2 — "the dark energy tweak is not there for some reason").
        var energy = _selected.GetComponentInChildren<EnemyDarkEnergy>(true);
        if (energy == null)
            GUILayout.Label("(no EnemyDarkEnergy anywhere on this enemy — prefab not wired for dark energy)");
        if (energy != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Energy {energy.CurrentEnergy:0.00}" +
                            (energy.BondBroken ? " CORRUPT" : energy.ComboUnlocked ? " combo" : ""),
                            GUILayout.Width(130));
            float e = GUILayout.HorizontalSlider(energy.CurrentEnergy, 0f, 1f);
            if (!Mathf.Approximately(e, energy.CurrentEnergy)) energy.DebugSetEnergy(e);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            energy.DebugFreezeGain = GUILayout.Toggle(energy.DebugFreezeGain, " Freeze gain (this enemy)");
            if (GUILayout.Button("Freeze ALL others", GUILayout.Width(130)))
                foreach (var g in _spawned)
                    if (g != null && g != _selected && g.activeInHierarchy)
                    {
                        var other = g.GetComponentInChildren<EnemyDarkEnergy>(true);
                        if (other != null) other.DebugFreezeGain = true;
                    }
            // Escape hatch for the freeze toggles (playtest round 2: "freeze needs unfreeze").
            if (GUILayout.Button("UNFREEZE all", GUILayout.Width(110)))
                foreach (var g in _spawned)
                    if (g != null && g.activeInHierarchy)
                    {
                        var other = g.GetComponentInChildren<EnemyDarkEnergy>(true);
                        if (other != null) other.DebugFreezeGain = false;
                    }
            GUILayout.EndHorizontal();
        }

 // Mood bench — TransitionTo drives Manpu autonomously: glyph pulse + held aura, MoodAmbient tint.
        GUILayout.Label("Mood (watch Manpu glyph + aura + tint):");
        var moodSys = _selected.GetComponent<EnemyMoodSystem>();
        if (moodSys == null) GUILayout.Label("(no EnemyMoodSystem on this enemy)");
        else
        {
            int perRow = 4, i = 0;
            var moods = (EnemyMood[])System.Enum.GetValues(typeof(EnemyMood));
            while (i < moods.Length)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < perRow && i < moods.Length; c++, i++)
                    if (GUILayout.Button(moods[i].ToString()))
                        moodSys.TransitionTo(moods[i], 0f, moods[i]);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label($"Current: {moodSys.CurrentMood}");
        }

        // Archetype force-behaviours (Severed grief-rage = pair mechanics; bench the aura via Enraged).
        GUILayout.Label("Force behaviour:");
        GUILayout.BeginHorizontal();
        if (_selected.GetComponent<SummonerEnemy>() is SummonerEnemy sm && GUILayout.Button("Summon"))
            sm.TriggerSummon();
        if (_selected.GetComponent<WitnessEnemy>() is WitnessEnemy w)
        {
            if (GUILayout.Button("Ritual")) w.StartRitual();
            if (GUILayout.Button("Bomb→twin"))
            {
                var t = NearestTwin(_selected.transform.position);
                if (t != null) w.ThrowBomb(t.transform);
            }
        }
        if (_selected.GetComponent<GroupGrabEnemy>() is GroupGrabEnemy gg && GUILayout.Button("Grab"))
            gg.StartGrab();
        // Force chain throw (user request 2026-07-10 — chain feel/reuse untestable without it):
        // target the nearest twin, then the REAL TryChainAttack path (windup marker → throw → grab/miss).
        if (_selected.GetComponent<TetherBreakerEnemy>() is TetherBreakerEnemy tb)
        {
            if (GUILayout.Button("Throw chain"))
            {
                var t = NearestTwin(_selected.transform.position);
                if (t != null) { enemy.SetTarget(t.transform); tb.TryChainAttack(); }
            }
            if (tb.ChainActive && GUILayout.Button("Release"))
                tb.Release();
        }
        // Force ghost (Siphon): real HandleSoulArrived path — needs the rescue soul out (Down a twin first).
        if (_selected.GetComponent<SiphonEnemy>() is SiphonEnemy si && GUILayout.Button("Force ghost"))
            si.TestTriggerGhostSpawn();
        GUILayout.EndHorizontal();

        // Live ghost controls (the ghost is a separate pooled object, not the selected Siphon).
        var ghosts = FindObjectsByType<SiphonGhost>(FindObjectsSortMode.None);
        foreach (var gh in ghosts)
        {
            if (!gh.gameObject.activeInHierarchy) continue;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Ghost: {gh.name}" + (gh.IsBinding ? " BINDING" : ""), GUILayout.Width(200));
            gh.DebugPauseBindTimer = GUILayout.Toggle(gh.DebugPauseBindTimer, " Pause bind timer");
            GUILayout.EndHorizontal();
        }
    }

    // Dummy mode: zero speed + attacks off, everything else (brain, mood, Manpu, perception) live.
    private void SetDummy(GameObject go, Enemy enemy, bool on)
    {
        if (on) _dummies.Add(go); else _dummies.Remove(go);
        enemy.Movement?.SetSpeed(on ? 0f : (enemy.Data != null ? enemy.Data.moveSpeed : 3.5f));
        var atk = enemy.AttackController;
        if (atk != null) atk.enabled = !on;
    }

    // ── POI bench (playtest round 2): spawn breakable POIs + watch the spawn-point recharge ──────
    // Prefab slots first (drag your L1_Park POIs into prefabs for authored visuals); when a slot is
    // empty a minimal primitive stand-in is built at runtime — real components, greybox look.
    private void DrawPoiSection()
    {
        GUILayout.Space(6);
        GUILayout.Label("── POI bench ──");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn point POI")) SpawnPoi(_spawnPointPoiPrefab, BuildSpawnPointPoi);
        if (GUILayout.Button("Ritual POI"))      SpawnPoi(_ritualPoiPrefab,     go => go.AddComponent<RitualSitePOI>());
        if (GUILayout.Button("Barrier POI"))     SpawnPoi(_barrierPoiPrefab,    go => go.AddComponent<BarrierPOI>());
        GUILayout.EndHorizontal();

        // Every live spawn point (spawned here OR scene-authored): break/recharge controls.
        var points = FindObjectsByType<SpawnPointPOI>(FindObjectsSortMode.None);
        foreach (var sp in points)
        {
            if (!sp.gameObject.activeInHierarchy) continue;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"SpawnPoint: {sp.name}  " +
                            (sp.IsRespawning ? $"recharging {sp.RechargeProgress:P0}" : sp.IsActive ? "ready" : "down"),
                            GUILayout.Width(280));
            // Force the destroyed state with 10 s left — the full recharge ramp, watchable immediately.
            if (GUILayout.Button("Recharge in 10s", GUILayout.Width(110)))
                sp.DebugSetRechargeRemaining(10f);
            GUILayout.EndHorizontal();
        }
    }

    private void SpawnPoi(GameObject prefab, System.Action<GameObject> buildFallback)
    {
        Vector3 pos = (_enemyPad != null ? _enemyPad.position : transform.position) + new Vector3(3f, 0f, 3f);
        if (prefab != null)
        {
            Instantiate(prefab, pos, Quaternion.identity);
            return;
        }
        // Greybox stand-in — real POI components on a primitive so behaviour is testable without art.
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "DebugPOI";
        go.transform.position = pos + Vector3.up * 0.5f;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) go.layer = enemyLayer;   // twin melee scans this layer — makes the POI hittable
        buildFallback(go);
        go.name = "DebugPOI_" + go.GetComponent<POIBase>()?.PoiType;
    }

    private static void BuildSpawnPointPoi(GameObject go)
    {
        go.AddComponent<SpawnPointPOI>();
        go.AddComponent<PoiDamageAdapter>();   // routes twin melee DamageData → SpawnPointPOI.TakeDamage
    }

    private void DrawPerceptionSection()
    {
        GUILayout.Space(6);
        GUILayout.Label("── Perception ──");
        if (_selected != null)
        {
            var listener = _selected.GetComponentInChildren<PerceptionListener>();
            if (listener != null)
            {
                bool senses = GUILayout.Toggle(listener.enabled, " Selected enemy senses (off = blind+deaf)");
                if (senses != listener.enabled) listener.enabled = senses;   // OnDisable deregisters (BUG-035 R8 pairing)
            }
        }
        bool detectable = GUILayout.Toggle(_twinsDetectable, " Twins detectable (off = invisible to ALL enemies)");
        if (detectable != _twinsDetectable)
        {
            _twinsDetectable = detectable;
            SetTwinPerceivable(_twins?.LeftTwin, detectable);
            SetTwinPerceivable(_twins?.RightTwin, detectable);
        }
    }

    private void DrawCueSection()
    {
        GUILayout.Space(6);
        GUILayout.Label("── Fire any cue (book + id) ──");

        // Code-ended FX escape hatch: held cues (mood auras, corruption state, looping ids fired below)
        // only stop when their OWNING code calls Stop — on a bench nothing ever does, so they linger.
        // (STOP ALL FX lives in the fixed global row; per-enemy sweep stays here.)
        if (_selected != null && GUILayout.Button("Stop FX on selected"))
            _fx?.StopAllOn(_selected.transform);

        if (_cueBooks.Count == 0) { GUILayout.Label("(no books — context menu: Auto-Fill Cue Books)"); return; }

        _bookIndex = Mathf.Clamp(_bookIndex, 0, _cueBooks.Count - 1);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", GUILayout.Width(30))) _bookIndex = (_bookIndex - 1 + _cueBooks.Count) % _cueBooks.Count;
        GUILayout.Label(_cueBooks[_bookIndex] ? _cueBooks[_bookIndex].name : "—");
        if (GUILayout.Button("▶", GUILayout.Width(30))) _bookIndex = (_bookIndex + 1) % _cueBooks.Count;
        GUILayout.EndHorizontal();

        var book = _cueBooks[_bookIndex];
        if (book == null) return;

        // Emitter-scale bench: fire any cue at footprint ×N to eyeball CueScalableEmitter plumbing
        // (at ×1 nothing changes by design — authored base = base gameplay radius).
        GUILayout.BeginHorizontal();
        GUILayout.Label($"emitterScale ×{_cueEmitterScale:0.0}", GUILayout.Width(120));
        _cueEmitterScale = GUILayout.HorizontalSlider(_cueEmitterScale, 0.5f, 4f);
        GUILayout.EndHorizontal();

        Transform anchor = _selected != null ? _selected.transform
                         : (_twinPad != null ? _twinPad : transform);
        foreach (var entry in book.effects)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
            if (GUILayout.Button(entry.id))
                _fx?.PlayBook(book, entry.id,   // on selected enemy, else the pad
                    CueContext.Follow(anchor, emitterScale: _cueEmitterScale));
        }
    }

    private float _cueEmitterScale = 1f;   // fire-any-cue footprint multiplier (bench-only)

    // Story-grading bench: drive SetStoryProgress with a slider (window grades pick automatically)
    // and fire any authored grade by id (event rows like "shock" included). This is also the answer
    // to "how do I use the grading" until story flags exist: gameplay calls the same two methods.
    private void DrawGradingSection()
    {
        GUILayout.Space(6);
        GUILayout.Label("── Story grading ──");
        var dir = StoryGradeDirector.Instance;
        if (dir == null) { GUILayout.Label("(no StoryGradeDirector — Persistent not loaded?)"); return; }

        GUILayout.BeginHorizontal();
        GUILayout.Label($"progress {_gradeProgress:0.00} → {dir.CurrentGradeId ?? "-"}", GUILayout.Width(190));
        float p = GUILayout.HorizontalSlider(_gradeProgress, 0f, 1f);
        GUILayout.EndHorizontal();
        if (!Mathf.Approximately(p, _gradeProgress))
        {
            _gradeProgress = p;
            dir.SetStoryProgress(p);
        }

        _gradeIds ??= dir.GradeIds();
        for (int i = 0; i < _gradeIds.Length; i += 3)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + 3, _gradeIds.Length); j++)
                if (GUILayout.Button(_gradeIds[j])) dir.PlayGrade(_gradeIds[j]);
            GUILayout.EndHorizontal();
        }
    }

    // World-state bench (user request 2026-07-24): play the two progression channels a StoryBeatTrigger
    // drives — corruption (WorldAmbienceDriver) and sky states (SkyStateDriver) — without placing a trigger.
    private void DrawWorldSkySection()
    {
        GUILayout.Space(6);
        GUILayout.Label("── World & Sky (corruption + sky states) ──");

        var world = WorldAmbienceDriver.Instance;
        if (world == null) GUILayout.Label("(no WorldAmbienceDriver — Persistent not loaded?)");
        else
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"corruption {world.Progress:0.00}", GUILayout.Width(150));
            float c = GUILayout.HorizontalSlider(world.Progress, 0f, 1f);
            GUILayout.EndHorizontal();
            if (!Mathf.Approximately(c, world.Progress)) world.SetProgress(c);
        }

        var sky = SkyStateDriver.Instance;
        if (sky == null) { GUILayout.Label("(no SkyStateDriver — Persistent not loaded?)"); return; }
        _skyIds ??= sky.StateIds();
        GUILayout.Label("Sky state (blends 3s):");
        for (int i = 0; i < _skyIds.Length; i += 3)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + 3, _skyIds.Length); j++)
                if (GUILayout.Button(_skyIds[j])) sky.BlendTo(_skyIds[j], 3f);
            GUILayout.EndHorizontal();
        }
    }

    // Grayscale world test (checklist item 21): desaturate the WHOLE screen so value/readability can be
    // judged with colour removed — enemy vs background, Kai vs Lyra, pickups, hazards must all still read.
    // Drives a global ColorAdjustments volume at max priority (beats every story/area grade). The volume
    // persists while amount > 0 even if the panel is hidden, so you can walk the world greyscaled.
    private void DrawGrayscaleSection()
    {
        GUILayout.Space(6);
        GUILayout.Label("── Grayscale world test (checklist item 21) ──");
        GUILayout.BeginHorizontal();
        GUILayout.Label($"grayscale {_grayscaleAmount:P0}", GUILayout.Width(110));
        float a = GUILayout.HorizontalSlider(_grayscaleAmount, 0f, 1f);
        if (GUILayout.Button("Full", GUILayout.Width(46))) a = 1f;
        if (GUILayout.Button("Off",  GUILayout.Width(46))) a = 0f;
        GUILayout.EndHorizontal();
        if (!Mathf.Approximately(a, _grayscaleAmount))
        {
            _grayscaleAmount = a;
            EnsureGrayscaleVolume();
            _grayscaleCA.saturation.value = Mathf.Lerp(0f, -100f, a);   // -100 = fully desaturated
            _grayscaleVolume.weight = a > 0.001f ? 1f : 0f;
        }
        GUILayout.Label("Anything that turns to mush needs a VALUE fix (lightness), not a hue change.");
    }

    // Lazily build the dev-owned global desaturation volume. Runtime-instance profile → dirties no asset;
    // layer 0 (Default) sits inside the MainCamera's volume mask; torn down in OnDestroy.
    private void EnsureGrayscaleVolume()
    {
        if (_grayscaleVolume != null) return;
        var go = new GameObject("~DebugGrayscaleVolume") { hideFlags = HideFlags.HideAndDontSave };
        _grayscaleVolume = go.AddComponent<Volume>();
        _grayscaleVolume.isGlobal = true;
        _grayscaleVolume.priority = 10000f;                              // outrank every story/area grade
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();  // instance-owned, not an asset
        _grayscaleVolume.sharedProfile = profile;
        _grayscaleCA = profile.Add<ColorAdjustments>(true);
        _grayscaleCA.saturation.overrideState = true;
    }

    private void OnDestroy()
    {
        if (_grayscaleVolume != null)
        {
            if (_grayscaleVolume.sharedProfile != null) Destroy(_grayscaleVolume.sharedProfile);
            Destroy(_grayscaleVolume.gameObject);
        }
    }

    private void DrawMetaSection()
    {
        GUILayout.Space(6);
        GUILayout.Label("── Meta ──");
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Skill points: {(_skillTree != null ? _skillTree.CurrentPoints : 0)}");
        if (GUILayout.Button("+1"))  _skillTree?.AddPoints(1);
        if (GUILayout.Button("+5"))  _skillTree?.AddPoints(5);
        if (GUILayout.Button("+20")) _skillTree?.AddPoints(20);
        GUILayout.EndHorizontal();

        // Ability bench cheats: everything instantly ready. Hard gating stays real —
        // Weaver's Gate still needs a twin in danger; SoulConv still needs its skill unlock.
        if (GUILayout.Button("Make abilities READY (cooldowns + SoulConv souls)"))
        {
            _twins?.LeftTwin?.GetComponent<AbilityController>()?.DebugClearCooldowns();
            _twins?.RightTwin?.GetComponent<AbilityController>()?.DebugClearCooldowns();
            SoulConvergenceSystem.Instance?.DebugFillSouls();
        }

        // (Teleport-twins + Kill ALL moved to the fixed global row, 2026-07-10 layout rework.
        //  TestLab sits OUTSIDE the streaming graph, so pad teleports need no NotifyTeleported.)
    }

    // ── helpers ────────────────────────────────────────────────────────────────
    private string ReadHp(GameObject g)
    {
        var h = g.GetComponent<EnemyHealthComponent>();
        return h != null ? $"{h.CurrentHealth:0}/{h.MaxHealth:0}" : "?";
    }

    private Player NearestTwin(Vector3 from)
    {
        var l = _twins?.LeftTwin; var r = _twins?.RightTwin;
        if (l == null) return r;
        if (r == null) return l;
        return (l.transform.position - from).sqrMagnitude <= (r.transform.position - from).sqrMagnitude ? l : r;
    }

    private static void SetTwinPerceivable(Player twin, bool detectable)
    {
        if (twin == null) return;
        var perceivable = twin.GetComponent<Perceivable>();
        if (perceivable != null) perceivable.enabled = detectable;   // OnDisable deregisters (BUG-035 pairing)
    }

    private static void TeleportTwin(Player twin, Vector3 pos)
    {
        if (twin == null) return;
        var cc = twin.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;   // CharacterController fights transform writes — the standard warp
        twin.transform.position = pos;
        if (cc != null) cc.enabled = true;
    }

#if UNITY_EDITOR
    // ── editor-only auto-fill (no editor asmdef in this project — #if UNITY_EDITOR is the pattern) ──────
    [ContextMenu("Auto-Fill Spawnables (enemy prefabs)")]
    private void AutoFillSpawnables()
    {
        _spawnables.Clear();
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets(
                     "t:Prefab", new[] { "Assets/Models/Prefabs/Enemies" }))
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponent<Enemy>() != null)
                _spawnables.Add(new SpawnEntry { label = prefab.name, prefab = prefab });
        }
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[GameDebuggerV2] Auto-filled {_spawnables.Count} enemy prefabs.", this);
    }

    [ContextMenu("Auto-Fill Cue Books")]
    private void AutoFillCueBooks()
    {
        _cueBooks.Clear();
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:CueBookData"))
        {
            var book = UnityEditor.AssetDatabase.LoadAssetAtPath<CueBookData>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (book != null) _cueBooks.Add(book);
        }
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[GameDebuggerV2] Auto-filled {_cueBooks.Count} cue books.", this);
    }
#endif
}
