using System;
using UnityEngine;

/// <summary>
/// Marks a serialized reference field that must NOT be null for the component to work.
/// The editor-side Validator (Tools ▸ Planet of Twins ▸ Validate) reports any
/// <c>[RequiredReference]</c> field left null in an open scene as an error.
///
/// This is a *documentation + lint* aid only — it has no runtime behaviour. Use it on
/// same-scene serialized slots that the object genuinely depends on (R1/R4 optional slots
/// that are meant to be filled). Do NOT use it to "require" a cross-scene ref — those must
/// resolve at runtime via <c>Manager.Instance</c> (R4), never be serialized (R2).
///
/// Example:
/// <code>
/// [RequiredReference, SerializeField] private BoxCollider _zoneVolume;
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class RequiredReferenceAttribute : PropertyAttribute
{
    /// <summary>Optional note shown alongside the validation error (why it's required).</summary>
    public string Note { get; }

    public RequiredReferenceAttribute(string note = null)
    {
        Note = note;
    }
}
