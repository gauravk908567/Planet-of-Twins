using System;
using System.Collections.Generic;
using CommonCore;
using PoT.Fx;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// P8.1: the Persistent managers the boot + restart tests check (CLAUDE.md "Key Persistent Singletons", plus
/// TimeScaleService and TwinInputReader). One probe per type: its static <c>Instance</c> (if it has one) and how many
/// live components of that type exist, inactive included.
/// Not listed: <c>GameBootstrapper</c> lives in Bootstrap, which unloads once boot hands over (it has no Instance).
/// </summary>
public static class PersistentSingletons
{
    public sealed class Probe
    {
        public readonly string Name;
        public readonly Func<Object> Instance;   // null = the type has no static Instance (count only)
        public readonly Func<int> Count;
        public readonly int ExpectedCount;
        /// <summary>Sealed AI core singleton (CLAUDE.md E1): DontDestroyOnLoad on purpose, so the same object
        /// outlives a Restart. Only its count is checked; its <c>Instance</c> getter would FABRICATE a blank
        /// manager if none existed, so the tests never touch it.</summary>
        public readonly bool SurvivesRestart;

        public Probe(string name, Func<Object> instance, Func<int> count, int expectedCount, bool survivesRestart)
        {
            Name = name;
            Instance = instance;
            Count = count;
            ExpectedCount = expectedCount;
            SurvivesRestart = survivesRestart;
        }
    }

    public static readonly Probe[] All =
    {
        With<SceneFlowManager>(() => SceneFlowManager.Instance),
        With<SaveService>(() => SaveService.Instance),
        With<WorldFlagRegistry>(() => WorldFlagRegistry.Instance),
        With<CheckpointManager>(() => CheckpointManager.Instance),
        With<SoftResetController>(() => SoftResetController.Instance),
        With<SkillTreeManager>(() => SkillTreeManager.Instance),
        With<PlayerRoster>(() => PlayerRoster.Instance),
        With<CouchDeviceManager>(() => CouchDeviceManager.Instance),
        With<EnemySpawner>(() => EnemySpawner.Instance),
        With<EnemyPool>(() => EnemyPool.Instance),
        With<QTEManager>(() => QTEManager.Instance),
        With<TutorialOverlayController>(() => TutorialOverlayController.Instance),
        With<TutorialHintDisplay>(() => TutorialHintDisplay.Instance),
        With<FailureResetSequencer>(() => FailureResetSequencer.Instance),
        With<FailureNotice>(() => FailureNotice.Instance),
        With<TimeFactorManager>(() => TimeFactorManager.Instance),
        With<RescueEventController>(() => RescueEventController.Instance),
        With<AccordStateSystem>(() => AccordStateSystem.Instance),
        With<LanguageManager>(() => LanguageManager.Instance),
        With<StoryGradeDirector>(() => StoryGradeDirector.Instance),
        With<WorldAmbienceDriver>(() => WorldAmbienceDriver.Instance),
        With<SkyStateDriver>(() => SkyStateDriver.Instance),
        With<FxManager>(() => FxManager.Instance),
        With<AudioManager>(() => AudioManager.Instance),
        With<MusicManager>(() => MusicManager.Instance),
        With<TimeScaleService>(() => TimeScaleService.Instance),
        // Two readers by design: P1 = the shared reader (Instance), P2 = the JSON clone.
        With<TwinInputReader>(() => TwinInputReader.Instance, expectedCount: 2),
        // No static Instance: exactly one, on QTEManager's GameObject.
        CountOnly<EnemyFreezeService>(),
        // The sealed AI core's perception manager: a duplicate or a blank fabricated one blinds every enemy (BUG-040).
        CountOnly<PerceptionManager>(survivesRestart: true),
    };

    /// <summary>Names of the probes whose static Instance is null (or a destroyed object) right now.</summary>
    public static List<string> MissingInstances()
    {
        var missing = new List<string>();
        foreach (var p in All)
            if (p.Instance != null && p.Instance() == null) missing.Add(p.Name);
        return missing;
    }

    /// <summary>Probe name → the Instance object's id, for every probe with a live Instance.</summary>
    public static Dictionary<string, int> CaptureInstanceIds()
    {
        var ids = new Dictionary<string, int>();
        foreach (var p in All)
        {
            if (p.Instance == null || p.SurvivesRestart) continue;
            var instance = p.Instance();
            if (instance != null) ids[p.Name] = instance.GetInstanceID();
        }
        return ids;
    }

    private static Probe With<T>(Func<T> instance, int expectedCount = 1) where T : Object =>
        new(typeof(T).Name, () => instance(), CountOf<T>, expectedCount, survivesRestart: false);

    private static Probe CountOnly<T>(bool survivesRestart = false) where T : Object =>
        new(typeof(T).Name, null, CountOf<T>, 1, survivesRestart);

    private static int CountOf<T>() where T : Object =>
        Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
}
