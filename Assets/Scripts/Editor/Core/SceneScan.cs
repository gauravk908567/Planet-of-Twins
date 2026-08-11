using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PlanetOfTwins.EditorTools
{
    /// <summary>
    /// Shared scene/serialized-reference scanning used by the Validator (cross-scene + null checks)
    /// and Area Auto-Wire (collecting same-scene objects). Pure read-only reflection over
    /// <see cref="SerializedObject"/> — no mutation, no YAML.
    ///
    /// The central distinction: a serialized UnityEngine.Object ref is either an **asset**
    /// (<see cref="EditorUtility.IsPersistent"/> — legal across scenes, R2) or a **scene object**
    /// (lives in a loaded scene). A scene-object ref whose owner is in a *different* scene is the
    /// R2 violation we hunt.
    /// </summary>
    public static class SceneScan
    {
        /// <summary>One serialized object-reference found on a component.</summary>
        public readonly struct RefHit
        {
            public readonly Component Owner;
            public readonly string PropertyPath;
            public readonly Object Target;

            public RefHit(Component owner, string propertyPath, Object target)
            {
                Owner = owner;
                PropertyPath = propertyPath;
                Target = target;
            }
        }

        /// <summary>True if the object is an on-disk asset (SO, prefab, material, clip…).</summary>
        public static bool IsPersistentAsset(Object o) => o != null && EditorUtility.IsPersistent(o);

        /// <summary>Resolve the scene a UnityEngine.Object lives in, if it is a scene object (not an asset).</summary>
        public static bool TryGetScene(Object o, out Scene scene)
        {
            scene = default;
            if (o == null || EditorUtility.IsPersistent(o)) return false;
            switch (o)
            {
                case Component c when c != null:
                    scene = c.gameObject.scene;
                    return scene.IsValid();
                case GameObject g:
                    scene = g.scene;
                    return scene.IsValid();
                default:
                    return false; // in-memory SO or similar — not a scene object
            }
        }

        public static bool IsSceneObject(Object o) => TryGetScene(o, out _);

        /// <summary>True when owner and target are both scene objects in DIFFERENT scenes (R2 violation).</summary>
        public static bool IsCrossScene(Object owner, Object target)
        {
            if (!TryGetScene(owner, out var ownerScene)) return false;
            if (!TryGetScene(target, out var targetScene)) return false;
            return ownerScene != targetScene;
        }

        /// <summary>Every non-null serialized object-reference on a component (descends arrays/lists/managed refs).</summary>
        public static IEnumerable<RefHit> SerializedRefs(Component component)
        {
            if (component == null) yield break;

            using var so = new SerializedObject(component);
            var prop = so.GetIterator();
            while (prop.NextVisible(true)) // enterChildren = descend into array elements / managed refs
            {
                if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (prop.name == "m_Script") continue; // the MonoScript asset itself
                var target = prop.objectReferenceValue;
                if (target == null) continue;
                yield return new RefHit(component, prop.propertyPath, target);
            }
        }

        /// <summary>All components in a loaded scene (roots + children, including inactive). Skips missing scripts.</summary>
        public static IEnumerable<Component> ComponentsIn(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            foreach (var root in scene.GetRootGameObjects())
            {
                var comps = root.GetComponentsInChildren<Component>(true);
                foreach (var c in comps)
                    if (c != null) yield return c;
            }
        }

        /// <summary>Cross-scene serialized-reference hits (R2) within one scene. Asset refs are skipped (legal).</summary>
        public static IEnumerable<RefHit> CrossSceneRefs(Scene scene)
        {
            foreach (var c in ComponentsIn(scene))
                foreach (var hit in SerializedRefs(c))
                {
                    if (IsPersistentAsset(hit.Target)) continue;
                    if (IsCrossScene(c, hit.Target))
                        yield return hit;
                }
        }

        /// <summary>Currently-loaded scenes, in editor order.</summary>
        public static IEnumerable<Scene> LoadedScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s.isLoaded) yield return s;
            }
        }
    }
}
