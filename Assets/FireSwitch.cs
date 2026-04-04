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

    // ─────────────────────────────────────────
    //  PRIVADOS
    // ─────────────────────────────────────────
    private bool hasBeenPressed = false;
    private Renderer switchRenderer;

    // ─────────────────────────────────────────────────────────────────────
    void Awake()
    {
        switchRenderer = GetComponent<Renderer>();
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
        if (hasBeenPressed) return;

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

        // Feedback visual del switch presionado
        OnSwitchPressed();
    }

    void OnSwitchPressed()
    {
        // Cambiar material si se asignó uno
        if (switchRenderer != null && pressedMaterial != null)
            switchRenderer.material = pressedMaterial;

        // Aquí puedes agregar:
        // - Animación de hundirse (transform.Translate)
        // - Sonido de click
        // - Partículas de confirmación
        Debug.Log("[FireSwitch] Switch ahora inactivo (no volverá a encenderse).");
    }
}