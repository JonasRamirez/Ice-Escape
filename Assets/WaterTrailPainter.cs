using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pinta manchas de agua en el tablero mientras el cubo se desliza.
/// Los decals son hijos del tablero, así rotan con el mapa.
///
/// SETUP:
///   1. Crea un Material con shader "Unlit/Transparent" (o Standard con
///      Rendering Mode = Transparent).
///   2. Asígnale una textura de mancha de agua (un círculo/blob blanco
///      con canal alpha, fondo negro o transparente).
///   3. Arrastra ese material al campo 'waterMaterial' en el Inspector.
///   4. Adjunta este script al GameObject del cubo.
/// </summary>
public class WaterTrailPainter : MonoBehaviour
{
    [Header("Material y textura")]
    [Tooltip("Material con textura de mancha de agua y Rendering Mode = Transparent")]
    public Material waterMaterial;

    [Header("Apariencia")]
    [Tooltip("Tamaño base del decal en unidades de mundo")]
    public float decalSize = 0.6f;

    [Tooltip("Opacidad máxima de la mancha (0 = invisible, 1 = sólido)")]
    [Range(0f, 1f)]
    public float maxAlpha = 0.55f;

    [Header("Frecuencia")]
    [Tooltip("Distancia mínima que debe recorrer el cubo para pintar una nueva mancha")]
    public float minDistance = 0.08f;

    [Header("Límite de instancias")]
    [Tooltip("Número máximo de manchas en escena. Las más antiguas se desvanecen.")]
    public int maxDecals = 200;

    [Tooltip("Tiempo en segundos que tarda en desvanecerse un decal antiguo cuando se llega al límite")]
    public float fadeOutTime = 1.5f;

    // ── Estado privado ──────────────────────────────────────────────────
    private CubeController cubeController;
    private GyroscopeSceneController boardController;
    private Transform boardTransform;

    private Vector3 lastPaintPosition;
    private bool initialized = false;

    // Cola de decals activos: (Transform del quad, MeshRenderer, tiempo de fade)
    private Queue<DecalEntry> decals = new Queue<DecalEntry>();

    private struct DecalEntry
    {
        public Transform tr;
        public MeshRenderer rend;
        public float fadeStartTime;   // -1 = no está en fade
        public Material instanceMat;  // material instanciado (para cambiar alpha sin afectar al original)
    }

    // ────────────────────────────────────────────────────────────────────
    void Start()
    {
        cubeController = GetComponent<CubeController>();
        if (cubeController == null)
        {
            Debug.LogError("[WaterTrailPainter] No se encontró CubeController en el mismo objeto.");
            enabled = false;
            return;
        }

        // Buscar el tablero igual que hace CubeController
        boardController = cubeController.boardController;
        if (boardController == null)
            boardController = FindObjectOfType<GyroscopeSceneController>();

        if (boardController == null)
        {
            Debug.LogError("[WaterTrailPainter] No se encontró GyroscopeSceneController.");
            enabled = false;
            return;
        }

        boardTransform = boardController.transform;
        lastPaintPosition = transform.position;
        initialized = true;
    }

    // ────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (!initialized || waterMaterial == null) return;

        // Solo pintar si el cubo toca el tablero
        // Accedemos al campo isGrounded mediante reflexión para no hacerlo público
        // — o más simple: lo hacemos interno. Ver nota al pie.
        if (!cubeController.IsGrounded) return;

        float dist = Vector3.Distance(transform.position, lastPaintPosition);
        if (dist < minDistance) return;

        PaintDecal();
        lastPaintPosition = transform.position;

        // Actualizar fades de decals que van a desaparecer
        UpdateFades();
    }

    // ────────────────────────────────────────────────────────────────────
    void PaintDecal()
    {
        // Si llegamos al límite, empezar a desvanecer el más antiguo
        if (decals.Count >= maxDecals)
        {
            DecalEntry oldest = decals.Dequeue();
            // Iniciar fade si no estaba ya en uno
            if (oldest.fadeStartTime < 0f)
            {
                oldest.fadeStartTime = Time.time;
                decals.Enqueue(oldest); // lo volvemos a meter al final para procesarlo
            }
            else
            {
                // Ya estaba en fade; destruir directamente
                if (oldest.tr != null)
                    Destroy(oldest.tr.gameObject);
            }
        }

        // ── Posición: proyectar sobre la superficie del tablero ──────────
        // El quad se coloca justo sobre el tablero, un pelo por encima
        // para evitar z-fighting.
        Vector3 boardNormal = boardTransform.up;
        Vector3 cubePos = transform.position;

        // Proyectamos la posición del cubo sobre el plano del tablero
        // (eliminamos la componente normal para pegarlo a la superficie)
        Vector3 toBoard = boardTransform.position - cubePos;
        float distToPlane = Vector3.Dot(toBoard, boardNormal);
        Vector3 onSurface = cubePos + boardNormal * (distToPlane + 0.2f);

        // ── Crear el quad ────────────────────────────────────────────────
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "WaterDecal";

        // Padre = tablero → rota con él
        quad.transform.SetParent(boardTransform, worldPositionStays: true);

        // Posición sobre la superficie
        quad.transform.position = onSurface;

        // Orientación: la cara del quad mira en la dirección de la normal del tablero
        // (mismo efecto que tumbarlo plano sobre la superficie)
        Quaternion baseRot = Quaternion.LookRotation(-boardNormal, boardTransform.forward);
        quad.transform.rotation = baseRot * Quaternion.Euler(0f, 0f, 90f);

        // Tamaño proporcional al cubo actual
        float size = decalSize * transform.localScale.x;
        quad.transform.localScale = new Vector3(size, size, size);

        // Quitar el collider (el quad lo trae por defecto)
        Destroy(quad.GetComponent<Collider>());

        // ── Material instanciado ─────────────────────────────────────────
        MeshRenderer rend = quad.GetComponent<MeshRenderer>();
        Material instanceMat = new Material(waterMaterial);
        Color col = instanceMat.color;
        col.a = maxAlpha;
        instanceMat.color = col;
        rend.material = instanceMat;

        // Registrar en la cola
        DecalEntry entry = new DecalEntry
        {
            tr = quad.transform,
            rend = rend,
            fadeStartTime = -1f,
            instanceMat = instanceMat
        };
        decals.Enqueue(entry);
    }

    // ────────────────────────────────────────────────────────────────────
    void UpdateFades()
    {
        // Iterar con lista temporal para poder modificar la cola
        int count = decals.Count;
        for (int i = 0; i < count; i++)
        {
            DecalEntry entry = decals.Dequeue();

            if (entry.fadeStartTime >= 0f && entry.tr != null)
            {
                float elapsed = Time.time - entry.fadeStartTime;
                float t = elapsed / fadeOutTime;

                if (t >= 1f)
                {
                    // Fade completado → destruir
                    if (entry.instanceMat != null) Destroy(entry.instanceMat);
                    Destroy(entry.tr.gameObject);
                    continue; // no re-encolar
                }

                // Reducir alpha gradualmente
                Color col = entry.instanceMat.color;
                col.a = Mathf.Lerp(maxAlpha, 0f, t);
                entry.instanceMat.color = col;
            }

            decals.Enqueue(entry); // re-encolar si sigue vivo
        }
    }

    // ────────────────────────────────────────────────────────────────────
    void OnDestroy()
    {
        // Limpiar materiales instanciados para no dejar leaks
        foreach (DecalEntry e in decals)
        {
            if (e.instanceMat != null) Destroy(e.instanceMat);
            if (e.tr != null) Destroy(e.tr.gameObject);
        }
        decals.Clear();
    }
}