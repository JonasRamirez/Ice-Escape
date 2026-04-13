using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coloca este componente en el botón/switch del laberinto.
/// Cuando el cubo (de tamaño suficiente) lo pisa, apaga todas las paredes
/// de fuego asignadas. Solo funciona una vez.
/// </summary>
public class FireSwitch : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  CONFIGURACIÓN
    // ─────────────────────────────────────────
    [Header("Paredes de fuego controladas")]
    [Tooltip("Arrastra aquí todos los FireWall que debe apagar este switch.")]
    public FireWall[] controlledWalls;

    [Header("Requisito de tamaño del cubo")]
    [Tooltip("Escala mínima que debe tener el cubo para activar el switch. " +
             "Si el cubo es más pequeño, el botón no responde.")]
    public float minimumCubeScale = 0.35f;

    [Header("Visual del switch")]
    [Tooltip("Material que se aplica al switch cuando se ha presionado (opcional).")]
    public Material pressedMaterial;

    [Header("Efectos de sonido")]
    [Tooltip("Sonido que se reproduce al presionar el switch.")]
    public AudioClip pressSound;

    [Range(0f, 1f)]
    public float soundVolume = 0.7f;

    [Header("Animación del switch")]
    [Tooltip("Duración de la animación de presionado en segundos.")]
    public float animationDuration = 0.3f;

    // ─────────────────────────────────────────
    //  PRIVADOS
    // ─────────────────────────────────────────
    private bool hasBeenPressed = false;
    private Renderer switchRenderer;
    private Vector3 originalScale;
    private Vector3 targetScale;
    private Color originalColor;
    private Color targetColor;
    private AudioSource audioSource;
    private bool isAnimating = false;
    private float animationTimer = 0f;

    // ─────────────────────────────────────────────────────────────────────
    void Awake()
    {
        switchRenderer = GetComponent<Renderer>();
        audioSource = GetComponent<AudioSource>();

        // Si no tiene AudioSource, se añade uno automáticamente
        if (audioSource == null && pressSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.volume = soundVolume;
        }

        // Guardar la escala original del botón (0.4, 0.15, 0.4 por defecto)
        originalScale = transform.localScale;
        targetScale = new Vector3(0.6f, 0.05f, 0.6f);

        // Guardar colores originales del material (si usa shader estándar)
        if (switchRenderer != null && switchRenderer.material != null)
        {
            if (switchRenderer.material.HasProperty("_Color"))
            {
                originalColor = switchRenderer.material.color;
                // Color más apagado (70% de brillo y saturación reducida)
                targetColor = new Color(
                    originalColor.r * 0.6f,
                    originalColor.g * 0.6f,
                    originalColor.b * 0.6f,
                    originalColor.a
                );
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  DETECCIÓN
    // ─────────────────────────────────────────────────────────────────────

    // Usar OnCollisionEnter si el switch NO es trigger
    void OnCollisionEnter(Collision collision)
    {
        TryPress(collision.gameObject);
    }

    // Usar OnTriggerEnter si el switch ES trigger (más común para botones)
    void OnTriggerEnter(Collider other)
    {
        TryPress(other.gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  LÓGICA PRINCIPAL
    // ─────────────────────────────────────────────────────────────────────
    void TryPress(GameObject other)
    {
        // El switch solo actúa una vez
        if (hasBeenPressed || isAnimating) return;

        CubeController cube = other.GetComponent<CubeController>();
        if (cube == null) return;

        // ── Verificación de tamaño ──────────────────────────────────────
        // Usamos la escala X del cubo como referencia de su tamaño actual.
        // (CubeController siempre aplica escala uniforme: Vector3.one * currentScale)
        float cubeScale = other.transform.localScale.x;

        if (cubeScale < minimumCubeScale)
        {
            return;
        }

        // ── Activar switch ──────────────────────────────────────────────
        hasBeenPressed = true;

        foreach (FireWall wall in controlledWalls)
        {
            if (wall != null)
                wall.Deactivate();
        }

        // Iniciar todos los efectos del switch presionado
        StartCoroutine(AnimateSwitchPress());
    }

    IEnumerator AnimateSwitchPress()
    {
        isAnimating = true;
        animationTimer = 0f;

        // Reproducir sonido si está asignado
        if (audioSource != null && pressSound != null)
        {
            audioSource.PlayOneShot(pressSound, soundVolume);
        }

        // Animación de escala y color
        while (animationTimer < animationDuration)
        {
            animationTimer += Time.deltaTime;
            float t = animationTimer / animationDuration;

            // Usar curva de easing para una animación más natural
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            // Interpolar escala
            transform.localScale = Vector3.Lerp(originalScale, targetScale, easedT);

            // Interpolar color del material si es posible
            if (switchRenderer != null && switchRenderer.material != null &&
                switchRenderer.material.HasProperty("_Color"))
            {
                switchRenderer.material.color = Color.Lerp(originalColor, targetColor, easedT);
            }

            yield return null;
        }

        // Asegurar que termina en los valores exactos
        transform.localScale = targetScale;

        if (switchRenderer != null && switchRenderer.material != null &&
            switchRenderer.material.HasProperty("_Color"))
        {
            switchRenderer.material.color = targetColor;
        }

        // Cambiar material si se asignó uno (opcional, sobreescribe el color)
        if (switchRenderer != null && pressedMaterial != null)
        {
            switchRenderer.material = pressedMaterial;
        }

        isAnimating = false;

        Debug.Log("[FireSwitch] Switch presionado - Efecto completado. No se puede volver atrás.");
    }
}