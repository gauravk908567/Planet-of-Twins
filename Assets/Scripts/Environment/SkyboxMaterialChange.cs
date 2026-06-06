using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class SkyboxMaterialChange : MonoBehaviour
{
    [SerializeField] private Material gameplaySkyboxMaterial;
    [SerializeField] private Color fogColor = new Color(0.255f, 0.082f, 0.384f, 1f);
    private void Start()
    {
        IntroCutSceneDirector();
    }
    void IntroCutSceneDirector()
    {
        if (gameObject.activeInHierarchy)
        {
            RenderSettings.skybox = gameplaySkyboxMaterial;
            RenderSettings.fog = true;
            RenderSettings.fogColor = fogColor;
        }
    }
}
