using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// FullScreenPassRendererFeature that runs ONLY for Game cameras (play mode + Game view).
/// The CoexistenceFog / SunShafts fullscreen shaders reconstruct world position from the
/// camera depth texture — in the SCENE VIEW the edit-mode depth texture is stale/mismatched
/// per pixel, so the fog paints the depth of objects BEHIND onto the pixels of objects in
/// FRONT: solid buildings appear transparent with back-silhouettes showing through
/// (2026-07-16 diagnosis; the user's "transparent buildings"). Preview/reflection cameras are
/// skipped for the same reason. Swap-in replacement: same inspector settings as the base
/// FullScreenPassRendererFeature (pass material, injection point, requirements).
/// </summary>
public class GameCameraFullScreenFeature : FullScreenPassRendererFeature
{
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game) return;   // scene view depth is unreliable
        base.AddRenderPasses(renderer, ref renderingData);
    }
}
