using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IceTrailPainter : MonoBehaviour
{
    [Header("Referencias")]
    public MeshRenderer boardRenderer;
    public GyroscopeSceneController boardController;

    [Header("Textura")]
    [Tooltip("Resolución de la textura de rastro (256/512/1024)")]
    public int trailResolution = 512;

    [Header("Pintura")]
    [Tooltip("Radio del círculo pintado cada frame (en texels)")]
    public int brushRadius = 6;

    [Tooltip("Intensidad del trazo (0-1). Más alto = más opaco")]
    [Range(0f, 1f)]
    public float brushOpacity = 0.85f;

    [Tooltip("Qué tan rápido se desvanece el rastro (0 = nunca, 1 = muy rápido)")]
    [Range(0f, 0.005f)]
    public float fadeSpeed = 0.0008f;

    [Tooltip("Color del agua/hielo derretido")]
    public Color trailColor = new Color(0.6f, 0.88f, 1f, 1f);

    // ── Privados ────────────────────────────────────────────────────
    private Texture2D trailTexture;
    private Color[] pixels;
    private Color[] fadeBuffer;
    private int texSize;
    private bool isDirty = false;

    // Para el fade acumulado
    private float fadeAccum = 0f;
    private const float FADE_INTERVAL = 0.05f; // fade cada 50ms, no cada frame

    void Start()
    {
        texSize = trailResolution;
        trailTexture = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        trailTexture.wrapMode = TextureWrapMode.Clamp;
        trailTexture.filterMode = FilterMode.Bilinear;

        // Inicializar transparente
        pixels = new Color[texSize * texSize];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);

        trailTexture.SetPixels(pixels);
        trailTexture.Apply();

        // Asignar al material del tablero como textura de rastro
        if (boardRenderer != null)
            boardRenderer.material.SetTexture("_TrailTex", trailTexture);

        // Buscar referencias si no están asignadas
        if (boardController == null)
            boardController = FindObjectOfType<GyroscopeSceneController>();

        if (boardRenderer == null && boardController != null)
            boardRenderer = boardController.GetComponent<MeshRenderer>();
    }

    void Update()
    {
        if (boardController == null || boardRenderer == null) return;

        PaintAtCurrentPosition();
        AccumulateFade();

        if (isDirty)
        {
            trailTexture.SetPixels(pixels);
            trailTexture.Apply(false); // false = no mipmaps, más rápido
            isDirty = false;
        }
    }

    void PaintAtCurrentPosition()
    {
        // Convertir posición mundo → UV del tablero
        Vector2 uv = WorldToTrailUV(transform.position);
        if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f) return;

        int cx = Mathf.RoundToInt(uv.x * (texSize - 1));
        int cy = Mathf.RoundToInt(uv.y * (texSize - 1));

        // Radio dinámico según tamaño del cubo
        float scale = transform.localScale.x;
        int radius = Mathf.Max(2, Mathf.RoundToInt(brushRadius * scale));

        PaintCircle(cx, cy, radius);
        isDirty = true;
    }

    void PaintCircle(int cx, int cy, int radius)
    {
        int r2 = radius * radius;

        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy > r2) continue;

                int px = cx + dx;
                int py = cy + dy;

                if (px < 0 || px >= texSize || py < 0 || py >= texSize) continue;

                int idx = py * texSize + px;

                // Pincel con falloff suave desde el centro
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float falloff = 1f - (dist / radius);
                falloff = falloff * falloff; // cuadrático = borde más suave

                float newAlpha = Mathf.Min(1f, pixels[idx].a + falloff * brushOpacity * Time.deltaTime * 60f);
                pixels[idx].a = newAlpha;
            }
        }
    }

    /// <summary>
    /// El fade se aplica a intervalos fijos para no hacerlo cada frame
    /// (ahorra mucho CPU en texturas grandes).
    /// </summary>
    void AccumulateFade()
    {
        if (fadeSpeed <= 0f) return;

        fadeAccum += Time.deltaTime;
        if (fadeAccum < FADE_INTERVAL) return;
        fadeAccum = 0f;

        float fadeDelta = fadeSpeed * FADE_INTERVAL * 200f;

        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a > 0f)
            {
                pixels[i].a = Mathf.Max(0f, pixels[i].a - fadeDelta);
                isDirty = true;
            }
        }
    }

    /// <summary>
    /// Convierte posición en mundo a coordenadas UV del tablero (0-1).
    /// Funciona con cualquier inclinación porque trabaja en espacio local del tablero.
    /// </summary>
    Vector2 WorldToTrailUV(Vector3 worldPos)
    {
        Transform board = boardController.transform;

        // Posición relativa al tablero en espacio local
        Vector3 localPos = board.InverseTransformPoint(worldPos);

        // Asumir que el tablero va de -boardHalf a +boardHalf en X y Z
        // Tomar los valores del CubeController si están disponibles
        float halfX = 4.5f;
        float halfZ = 4.5f;

        CubeController cc = GetComponent<CubeController>();
        if (cc != null) { halfX = cc.boardHalfX; halfZ = cc.boardHalfZ; }

        float u = (localPos.x + halfX) / (halfX * 2f);
        float v = (localPos.z + halfZ) / (halfZ * 2f);

        return new Vector2(u, v);
    }

    void OnDestroy()
    {
        if (trailTexture != null)
            Destroy(trailTexture);
    }
}