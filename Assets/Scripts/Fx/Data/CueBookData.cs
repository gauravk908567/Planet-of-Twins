using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Cue Book — ONE asset that is the CONTAINER of a thing's effects (an
/// ability, an enemy, a trap, the player's locomotion set, Manpu…). It holds a list of NAMED effects; each
/// effect has a string <see cref="CueEntry.id"/> and an ordered list of inline <see cref="CueElement"/>s
/// (visuals, each with its own per-element audio). Gameplay code plays the CORRECT effect by id —
/// <c>FxManager.PlayBook(book, "id", ctx)</c> — never the whole book. CONFIG ONLY (R7).
///
/// The id is a plain string (a name local to this book, not a global enum): the per-call-site verifier
/// (Tools ▸ Planet of Twins) flags any id a script plays that no book defines, plus duplicate ids.
/// </summary>
[CreateAssetMenu(menuName = "PlanetOfTwins/Fx/Cue Book", fileName = "CueBook_")]
public class CueBookData : ScriptableObject
{
    [Serializable]
    public class CueEntry
    {
        [Tooltip("The string identifier code plays this effect by: FxManager.PlayBook(book, id, ctx).")]
        public string id;
        [Tooltip("Ordered visual elements (each carries its own audio), played together with timing + cut.")]
        public List<CueElement> elements = new List<CueElement>();
    }

    [Tooltip("Defines the dt domain the element list is SEQUENCED on (delays/cuts): Scaled = slows under " +
             "Setsuna; Unscaled = pause-proof (UI books). Each element's own timeMode still controls how " +
             "its spawned instance runs (R10).")]
    public TimeMode timeMode = TimeMode.Scaled;

    [Tooltip("The named effects this book contains — one entry per callable id.")]
    public List<CueEntry> effects = new List<CueEntry>();

    /// <summary>Elements of the effect named <paramref name="id"/>, or null if this book has no such id.</summary>
    public IReadOnlyList<CueElement> GetElements(string id)
    {
        if (effects == null || string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < effects.Count; i++)
            if (effects[i] != null && effects[i].id == id)
                return effects[i].elements;
        return null;
    }

    public bool Has(string id) => GetElements(id) != null;
}
