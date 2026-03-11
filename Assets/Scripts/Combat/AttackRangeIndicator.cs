using UnityEngine;

public class AttackRangeIndicator : MonoBehaviour
{
    [Header("Circle settings")]
    [SerializeField] private int segments = 32;
    [SerializeField] private float yOffset = 0.05f; // just above ground

    [Header("Colors")]
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0f);    // invisible
    [SerializeField] private Color windupColor = new Color(1f, 0.3f, 0.3f, 0.8f); // red

    private LineRenderer _lr;
    private float _currentRadius;

    private void Awake()
    {
        _lr = gameObject.AddComponent<LineRenderer>();
        _lr.loop = true;
        _lr.useWorldSpace = false;
        _lr.widthMultiplier = 0.05f;
        _lr.positionCount = segments;

        // Unlit material so it's always visible regardless of lighting
        _lr.material = new Material(Shader.Find("Sprites/Default"));
        _lr.startColor = idleColor;
        _lr.endColor = idleColor;

        Hide();
    }

    public void Show(float radius)
    {
        _currentRadius = radius;
        _lr.enabled = true;
        _lr.startColor = windupColor;
        _lr.endColor = windupColor;
        DrawCircle(radius);
    }

    public void Hide()
    {
        _lr.enabled = false;
    }

    private void DrawCircle(float radius)
    {
        _lr.positionCount = segments;
        float angleStep = 360f / segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            _lr.SetPosition(i, new Vector3(x, yOffset, z));
        }
    }
}
