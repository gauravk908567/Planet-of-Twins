using System;

/// <summary>
/// P19 seam 1: the ONLY scene-flow knowledge the Fx package needs, declared
/// package-side so `Fx/` never names a game type. The game's scene streamer (SceneFlowManager)
/// implements this and is wired into FxManager / MusicManager via their serialized slots
/// (R1 — all three live in Persistent).
///
///   • <see cref="OnSceneWillUnload"/> — fired with the unloading SCENE NAME before an area
///     unloads (FxManager's F1 reclaim contract: stop + reclaim every live instance whose
///     follow target lives in that scene).
///   • <see cref="OnLocationAudioChanged"/> — fired when the active location changes, carrying
///     that location's music + ambience (both Fx package types; MusicManager crossfades).
/// </summary>
public interface IFxSceneEvents
{
    event Action<string> OnSceneWillUnload;
    event Action<MusicTrackData, AmbienceData> OnLocationAudioChanged;

    /// <summary>The CURRENT active location's audio (null when none) — MusicManager pulls this once
    /// at Start so a late subscriber doesn't miss the initial location.</summary>
    MusicTrackData CurrentMusicTrack { get; }
    AmbienceData CurrentAmbience { get; }
}
