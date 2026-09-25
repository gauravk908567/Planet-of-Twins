using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The ASSET dependencies the settings handlers need: the audio mixer, the URP pipeline asset, the
/// renderer data holding the SSAO / shafts features, the volumetric-fog volume profile, and the main
/// camera. These are ASSET references, not UI-control references, so serializing them is legitimate
/// and stable (R1) — the fragility this system removes is references to specific control GameObjects,
/// never to assets. The SettingsScreenController owns the serialized slots and hands a populated
/// config to each handler, so no handler resolves assets itself.
/// </summary>
public sealed class SettingsBackendConfig
{
    public AudioMixer AudioMixer;
    public UniversalRenderPipelineAsset UrpAsset;
    public ScriptableRendererData RendererData;
    public Material FogMaterial;   // legacy PoT-fog material slot (retained, no longer driven — see GraphicsSettingsHandler)
    public VolumeProfile FogProfile;   // the live CristianQiu VolumetricFog profile (FogVolume in Persistent) the Fog row drives
    public Camera MainCamera;
}
