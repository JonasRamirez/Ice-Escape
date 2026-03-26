using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EndGoal : MonoBehaviour
{
    private Text completadoText;
    private bool gameFinished = false;

    // Variables para la transición
    private float waitTime = 2f; // Tiempo que se muestra "Completado" antes de cambiar de escena
    private float timer = 0f;
    private bool levelCompleted = false;

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
        completadoText.text = "¡Nivel Completado!";
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
        levelCompleted = true;

        // Detener encogimiento — activar flag público en CubeController
        if (cube != null)
            cube.reachedGoal = true;

        // Mostrar texto
        if (completadoText != null)
            completadoText.enabled = true;

        // Pausar el movimiento pero permitir que el timer funcione
        Time.timeScale = 0f;

        // Guardar progreso (desbloquear siguiente nivel)
        SaveProgress();
    }

    void SaveProgress()
    {
        // Obtener el nivel actual desde el nombre de la escena
        string sceneName = Application.loadedLevelName;
        int currentLevel = ExtractLevelNumber(sceneName);

        // Desbloquear el siguiente nivel
        int nextLevel = currentLevel + 1;

        if (nextLevel <= 5)
        {
            // Guardar en PlayerPrefs qué niveles están desbloqueados
            if (PlayerPrefs.GetInt("Level" + nextLevel + "_Unlocked", 0) == 0)
            {
                PlayerPrefs.SetInt("Level" + nextLevel + "_Unlocked", 1);
                PlayerPrefs.Save();
                Debug.Log("¡Nivel " + nextLevel + " desbloqueado!");
            }
        }

        // Marcar nivel actual como completado
        PlayerPrefs.SetInt("Level" + currentLevel + "_Completed", 1);
        PlayerPrefs.Save();
    }

    int ExtractLevelNumber(string sceneName)
    {
        // Extraer el número del nombre de la escena (ej: "level1" -> 1)
        string numberPart = sceneName.Replace("level", "");
        int levelNumber;
        if (int.TryParse(numberPart, out levelNumber))
        {
            return levelNumber;
        }
        return 1; // Por defecto, nivel 1
    }

    void Update()
    {
        // Si el nivel está completado, manejar el timer
        if (levelCompleted && Time.timeScale == 0f)
        {
            // Time.unscaledDeltaTime funciona incluso con timeScale = 0
            timer += Time.unscaledDeltaTime;

            if (timer >= waitTime)
            {
                // Restaurar timeScale antes de cargar la nueva escena
                Time.timeScale = 1f;

                // Cargar el selector de niveles
                Application.LoadLevel("levelselectorscene");
            }
        }
    }
}