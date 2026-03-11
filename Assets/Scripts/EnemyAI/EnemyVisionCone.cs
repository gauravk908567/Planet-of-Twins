using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class EnemyVisionCone : MonoBehaviour
{
    [Header("Cone shape")]
    [SerializeField] private float viewAngle = 90f;  // total angle in degrees
    [SerializeField] private float viewRadius = 8f;   // matches detectionRange
    [SerializeField] private int rayCount = 24;   // smoothness of arc
    [SerializeField] private float yOffset = 0.05f;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(1f, 0.9f, 0f, 0.25f);  // yellow
    [SerializeField] private Color alertColor = new Color(1f, 0.2f, 0.2f, 0.4f); // red

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _mesh;
    private Material _material;
    private bool _isAlert;

    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();

        _mesh = new Mesh { name = "VisionCone" };
        _meshFilter.mesh = _mesh;

        _material = new Material(Shader.Find("Sprites/Default"));
        _material.color = normalColor;
        _meshRenderer.material = _material;
        _meshRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;
    }

    private void Start()
    {
        // Auto-read detectionRange from EnemyDetection if available
        var detection = GetComponent<EnemyDetection>();
        if (detection != null)
            viewRadius = detection.DetectionRange;
    }

    private void LateUpdate()
    {
        // Rebuilt every frame so it rotates with the enemy
        BuildMesh();
    }

    public void SetAlert(bool alert)
    {
        _isAlert = alert;
        if (_material != null)
            _material.color = alert ? alertColor : normalColor;
    }

    // Called by EnemySpawner/ApplyData to match EnemyData.detectionRange
    public void SetRadius(float radius) => viewRadius = radius;

    private void BuildMesh()
    {
        int vertCount = rayCount + 2; // origin + arc points
        var verts = new Vector3[vertCount];
        var tris = new int[rayCount * 3];

        // Origin at local (0, yOffset, 0)
        verts[0] = new Vector3(0, yOffset, 0);

        float halfAngle = viewAngle * 0.5f;
        float angleStep = viewAngle / rayCount;

        for (int i = 0; i <= rayCount; i++)
        {
            // Angle in local space — 0 = forward (local Z+)
            float angle = (-halfAngle + angleStep * i) * Mathf.Deg2Rad;
            float x = Mathf.Sin(angle) * viewRadius;
            float z = Mathf.Cos(angle) * viewRadius;
            verts[i + 1] = new Vector3(x, yOffset, z);
        }

        for (int i = 0; i < rayCount; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = i + 2;
        }

        _mesh.Clear();
        _mesh.vertices = verts;
        _mesh.triangles = tris;
        _mesh.RecalculateNormals();
    }

    public bool IsTargetVisible(Transform target)
    {
        if (target == null) return false;
        // Reuse whatever LoS check you already have in the vision cone
        // Simple version if you don't have one yet:
        Vector3 dir = target.position - transform.position;
        float angle = Vector3.Angle(transform.forward, dir);
        return angle <= viewAngle * 0.5f;
    }

    // Call from EnemyChaseState / EnemyAttackState to turn red
    // and from EnemyIdleState to turn yellow
    private void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
        if (_material != null) Destroy(_material);
    }
}