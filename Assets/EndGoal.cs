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
    private GameManager gameManager;

    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
        CreateCompletadoUI();
    }

    GameObject completadoCanvas;

    void CreateButton(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonGO = new GameObject(text);
        buttonGO.transform.SetParent(parent, false);

        Image img = buttonGO.AddComponent<Image>();
        img.color = Color.white;

        Button btn = buttonGO.AddComponent<Button>();
        btn.onClick.AddListener(action);

        RectTransform rect = buttonGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 60);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;

        // Texto del botón
        GameObject txtGO = new GameObject("Text");
        txtGO.transform.SetParent(buttonGO.transform, false);

        Text txt = txtGO.AddComponent<Text>();
        txt.text = text;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.black;
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        RectTransform txtRect = txtGO.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;
    }

    void CreateCompletadoUI()
    {
        // Canvas
        GameObject canvasGO = new GameObject("Canvas_Completado");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        canvasGO.AddComponent<GraphicRaycaster>();

        // =========================
        // 🔳 FONDO OSCURO
        // =========================
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);

        Image bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.6f); // Negro transparente

        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // =========================
        // 📝 TEXTO
        // =========================
        GameObject textGO = new GameObject("Texto_Completado");
        textGO.transform.SetParent(canvasGO.transform, false);

        completadoText = textGO.AddComponent<Text>();
        completadoText.text = "Nivel Completado!";
        completadoText.fontSize = 60;
        completadoText.alignment = TextAnchor.MiddleCenter;
        completadoText.color = Color.white;
        completadoText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.7f);
        textRect.anchorMax = new Vector2(0.5f, 0.7f);
        textRect.sizeDelta = new Vector2(600, 100);
        textRect.anchoredPosition = Vector2.zero;

        // =========================
        // 🔁 BOTÓN REINTENTAR
        // =========================
        CreateButton(canvasGO.transform, "Reintentar", new Vector2(0, -50), RestartLevel);

        // =========================
        // ▶ BOTÓN CONTINUAR
        // =========================
        CreateButton(canvasGO.transform, "Continuar", new Vector2(0, -130), NextLevel);

        // Oculto al inicio
        canvasGO.SetActive(false);

        // Guardamos referencia
        completadoCanvas = canvasGO;
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

        if (cube != null)
            cube.reachedGoal = true;

        // Mostrar UI
        completadoCanvas.SetActive(true);

        // Pausar juego
        Time.timeScale = 0f;

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

    void RestartLevel()
    {
        Time.timeScale = 1f;
        Application.LoadLevel(Application.loadedLevelName);
    }

    void NextLevel()
    {
        gameManager.CompleteLevel();
        Time.timeScale = 1f;
    }
}