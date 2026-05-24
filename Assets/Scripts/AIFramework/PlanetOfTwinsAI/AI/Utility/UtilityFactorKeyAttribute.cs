using UnityEngine;

/// <summary>
/// Attribute to mark a string field as a UtilityFactorKey.
/// Shows a dropdown in Inspector populated from UtilityFactorKeys constants.
/// Usage: [UtilityFactorKey] public string blackboardKey;
/// </summary>
public class UtilityFactorKeyAttribute : PropertyAttribute { }