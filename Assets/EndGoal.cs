using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EndGoal : MonoBehaviour
{
    private Text completadoText;
    private bool gameFinished = false;

    void Start()
    {
        CreateCompletadoUI();
    }

    void CreateCompletadoUI()
    {
        // Crear Canvas
        GameObject canvasGO = new GameObject("Canvas_Completado");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Crear Text
        GameObject textGO = new GameObject("Texto_Completado");
        textGO.transform.SetParent(canvasGO.transform, false);

        completadoText = textGO.AddComponent<Text>();
        completadoText.text = "¡Completado!";
        completadoText.fontSize = 72;
        completadoText.fontStyle = FontStyle.Bold;
        completadoText.alignment = TextAnchor.MiddleCenter;
        completadoText.color = new Color(0.2f, 1f, 0.3f, 1f); // Verde

        // Posición centrada en pantalla
        RectTransform rect = textGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Oculto al inicio
        completadoText.enabled = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (gameFinished) return;

        Debug.Log("[EndGoal] Trigger tocado por: " + other.gameObject.name);

        CubeController cube = other.GetComponent<CubeController>();
        if (cube != null)
        {
            Debug.Log("[EndGoal] ¡Cubo detectado! Nivel completado.");
            FinishGame(cube);
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (gameFinished) return;

        CubeController cube = other.GetComponent<CubeController>();
        if (cube != null)
        {
            FinishGame(cube);
        }
    }

    void FinishGame(CubeController cube)
    {
        gameFinished = true;

        // Detener encogimiento — activar flag público en CubeController
        cube.reachedGoal = true;

        // Mostrar texto
        if (completadoText != null)
            completadoText.enabled = true;

        // Pausar juego usando unscaledTime para que el texto siga visible
        Time.timeScale = 0f;
    }
}