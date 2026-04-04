using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coloca este componente en cada GameObject de pared de fuego.
/// La pared arranca activa y solo se puede apagar (nunca volver a prender).
/// Cuando el cubo la toca, le quita una porción drástica de su escala actual.
/// </summary>
public class FireWall : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  CONFIGURACIÓN
    // ─────────────────────────────────────────
    [Header("Daño al cubo")]
    [Tooltip("Fracción de la escala actual que se le quita al tocar la pared (0-1). " +
             "Ej: 0.6 = pierde el 60% de su tamaño actual.")]
    [Range(0f, 0.99f)]
    public float scalePenaltyFraction = 0.6f;

    [Tooltip("Tiempo mínimo entre penalizaciones (evita que el daño se aplique " +
             "múltiples veces en el mismo frame de colisión).")]
    public float damageCooldown = 0.5f;

    // ─────────────────────────────────────────
    //  PRIVADOS
    // ─────────────────────────────────────────
    private bool isActive = true;       // La pared empieza encendida
    private float lastDamageTime = -999f;

    // Renderer/Collider cacheados para no buscarlos cada frame
    private Renderer wallRenderer;
    private Collider wallCollider;

    // ─────────────────────────────────────────────────────────────────────
    void Awake()
    {
        wallRenderer = GetComponent<Renderer>();
        wallCollider = GetComponent<Collider>();

        // Aseguramos estado inicial
        SetActive(true);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  API PÚBLICA — la llama el botón/switch
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Apaga la pared de fuego. Solo funciona en una dirección.
    /// Una vez apagada no puede volver a encenderse.
    /// </summary>
    public void Deactivate()
    {
        if (!isActive) return;
        SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  COLISIONES
    // ─────────────────────────────────────────────────────────────────────
    void OnCollisionEnter(Collision collision)
    {
        if (!isActive) return;
        TryDamageCube(collision.gameObject);
    }

    // OnTriggerEnter por si la pared usa Is Trigger
    void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;
        TryDamageCube(other.gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  INTERNO
    // ─────────────────────────────────────────────────────────────────────
    void TryDamageCube(GameObject other)
    {
        // Cooldown para evitar multi-hit en el mismo instante
        if (Time.time - lastDamageTime < damageCooldown) return;

        CubeController cube = other.GetComponent<CubeController>();
        if (cube == null) return;

        cube.ApplyFireDamage(scalePenaltyFraction);
        lastDamageTime = Time.time;
        
    }

    void SetActive(bool active)
    {
        isActive = active;

        // Visual: apagar/encender el renderer
        if (wallRenderer != null)
            wallRenderer.enabled = active;

        // Colisión: apagar/encender el collider
        if (wallCollider != null)
            wallCollider.enabled = active;

        // Aquí puedes agregar efectos de partículas, audio, etc.
        // particleSystem.Stop() / particleSystem.Play()
    }
}