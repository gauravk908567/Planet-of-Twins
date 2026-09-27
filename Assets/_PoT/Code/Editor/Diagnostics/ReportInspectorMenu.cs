using PoT.Diagnostics.Editor;
using UnityEditor;

/// <summary>The Report Inspector under the project's tools menu (the package also lists it under Window ▸ Analysis).</summary>
public static class ReportInspectorMenu
{
    [MenuItem("Planet of Twins Tools/Diagnostics/Report Inspector")]
    private static void Open() => ReportInspectorWindow.Open();
}
