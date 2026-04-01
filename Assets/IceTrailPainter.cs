using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine;

/// <summary>
/// Pinta un rastro de agua continuo sobre el tablero.
/// Usa un quad hijo del tablero con Sprites/Default — sin shader custom.
/// El quad hereda la rotación del tablero automáticamente.
/// </summary>
public class IceTrailPainter : MonoBehaviour
{
    [Header("Referencias")]
    public GyroscopeSceneController boardController;

    [Header("Dimensiones del tablero")]
    public float boardHalfX = 4.5f;
    public float boardHalfZ = 4.5f;

    [Header("Textura")]
    [Tooltip("512 es suficiente para buen rendimiento")]
    public int trailResolution = 512;

    [Header("Pincel")]
    public int brushRadius = 7;

    [Range(0f, 1f)]
    public float brushOpacity = 0.9f;

    [Tooltip("0 = permanente, valores pequeños = se desvanece lento")]
    [Range(0f, 0.003f)]
    public float fadeSpeed = 0.001f;

    public Color trailColor = new Color(0.55f, 0.85f, 1f, 1f);

    // ── Privados ─────────────────────────────────────────────────────
    private Texture2D trailTex;
    private Color[] pixels;
    private bool isDirty = false;
    private float fadeAccum = 0f;
    private const float FADE_INTERVAL = 0.06f;
    private int texSize;

    void Start()
    {
        if (boardController == null)
            boardController = FindObjectOfType<GyroscopeSceneController>();

        texSize = trailResolution;

        // Crear textura
        trailTex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        trailTex.wrapMode = TextureWrapMode.Clamp;
        trailTex.filterMode = FilterMode.Bilinear;

        pixels = new Color[texSize * texSize];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);

        trailTex.SetPixels(pixels);
        trailTex.Apply();

        CreateTrailQuad();
    }

    void CreateTrailQuad()
    {
        // Crear un quad hijo del tablero — hereda su rotación y posición
        GameObject quad = new GameObject("IceTrailQuad");
        quad.transform.SetParent(boardController.transform);

        // Posicionarlo justo sobre la superficie del tablero
        // Y local ligeramente positivo para no hacer z-fighting
        quad.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        quad.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        quad.transform.localScale = new Vector3(boardHalfX * 2f, boardHalfZ * 2f, 1f);

        // El quad por defecto de Unity está en el plano XY,
        // necesitamos rotarlo para que quede en XZ (plano horizontal del tablero)
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        MeshFilter mf = quad.AddComponent<MeshFilter>();
        MeshRenderer mr = quad.AddComponent<MeshRenderer>();

        mf.mesh = CreateQuadMesh();

        // Material transparente simple — sin shader custom
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = trailTex;
        mat.color = Color.white;

        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // Asegurarse que se renderiza encima del tablero
        mr.sortingOrder = 1;
    }

    Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f)
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };
        mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        return mesh;
    }

    void Update()
    {
        if (boardController == null) return;

        PaintAtCurrentPosition();
        AccumulateFade();

        if (isDirty)
        {
            trailTex.SetPixels(pixels);
            trailTex.Apply(false);
            isDirty = false;
        }
    }

    void PaintAtCurrentPosition()
    {
        Vector2 uv = WorldToUV(transform.position);

        // Si el cubo está fuera del tablero, no pintar
        if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f) return;

        int cx = Mathf.RoundToInt(uv.x * (texSize - 1));
        int cy = Mathf.RoundToInt(uv.y * (texSize - 1));

        // Radio proporcional al tamaño actual del cubo
        float cubeScale = transform.localScale.x;
        int radius = Mathf.Max(2, Mathf.RoundToInt(brushRadius * cubeScale));

        PaintCircle(cx, cy, radius);
        isDirty = true;
    }

    void PaintCircle(int cx, int cy, int radius)
    {
        int r2 = radius * radius;
        float opacity = brushOpacity * Time.deltaTime * 60f;

        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy > r2) continue;

                int px = cx + dx;
                int py = cy + dy;
                if (px < 0 || px >= texSize || py < 0 || py >= texSize) continue;

                int idx = py * texSize + px;

                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float falloff = 1f - (dist / radius);
                falloff *= falloff; // borde suave

                pixels[idx].a = Mathf.Min(1f, pixels[idx].a + falloff * opacity);
            }
        }
    }

    void AccumulateFade()
    {
        if (fadeSpeed <= 0f) return;

        fadeAccum += Time.deltaTime;
        if (fadeAccum < FADE_INTERVAL) return;
        fadeAccum = 0f;

        float delta = fadeSpeed * FADE_INTERVAL * 200f;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a > 0f)
            {
                pixels[i].a = Mathf.Max(0f, pixels[i].a - delta);
                isDirty = true;
            }
        }
    }

    /// <summary>
    /// Convierte posición mundo → UV (0-1) relativa al tablero.
    /// Trabaja en espacio local del tablero, así funciona con cualquier inclinación.
    /// </summary>
    Vector2 WorldToUV(Vector3 worldPos)
    {
        Vector3 local = boardController.transform.InverseTransformPoint(worldPos);
        float u = (local.x + boardHalfX) / (boardHalfX * 2f);
        float v = (local.z + boardHalfZ) / (boardHalfZ * 2f);
        return new Vector2(u, v);
    }

    void OnDestroy()
    {
        if (trailTex != null) Destroy(trailTex);
    }
}