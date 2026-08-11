using UnityEngine;

/// <summary>
/// Shader-agnostic body tinting. Enemy / trap / ghost / mood state tints (stun, possess, rage,
/// ritual, crush, grabbed, mood aura) historically wrote <c>renderer.material.color</c> — i.e. the
/// legacy built-in <c>_Color</c> property. URP shaders and the project's <c>PoT/Coexistence</c>
/// shader expose <c>_BaseColor</c> instead and have NO <c>_Color</c>, so <c>.color</c> throws
/// "Material '…' doesn't have a color property '_Color'". These helpers resolve whichever colour
/// property the material actually exposes (<c>_BaseColor</c> preferred, <c>_Color</c> fallback) so a
/// tint works on ANY shader without error spam. No-op when the material has no tintable colour.
/// </summary>
public static class MaterialTint
{
    private static readonly int BaseColorId   = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorId = Shader.PropertyToID("_Color");

    /// <summary>Id of the colour property this material exposes, or -1 if it has neither.</summary>
    public static int ColorPropertyId(Material mat)
    {
        if (mat == null) return -1;
        if (mat.HasProperty(BaseColorId)) return BaseColorId;
        if (mat.HasProperty(LegacyColorId)) return LegacyColorId;
        return -1;
    }

    /// <summary>Read the material's tint colour through whichever property it exposes (white if none).</summary>
    public static Color GetColor(Material mat)
    {
        int id = ColorPropertyId(mat);
        return id != -1 ? mat.GetColor(id) : Color.white;
    }

    /// <summary>Write the material's tint colour through whichever property it exposes (no-op if none).</summary>
    public static void SetColor(Material mat, Color c)
    {
        int id = ColorPropertyId(mat);
        if (id != -1) mat.SetColor(id, c);
    }
}
