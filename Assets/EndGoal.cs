using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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

    private GameObject completadoCanvas;

    void CreateButton(Transform parent, string buttonText, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonGO = new GameObject("Btn_" + buttonText);
        buttonGO.transform.SetParent(parent, false);

        // Imagen del botón
        Image btnImage = buttonGO.AddComponent<Image>();
        btnImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // Componente Button
        Button button = buttonGO.AddComponent<Button>();
        button.onClick.AddListener(action);

        // Transición de colores
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        colors.highlightedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        colors.pressedColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        button.colors = colors;

        RectTransform btnRect = buttonGO.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = new Vector2(300, 60);
        btnRect.anchoredPosition = position;

        // Texto del botón
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);

        Text text = textGO.AddComponent<Text>();
        text.text = buttonText;
        text.fontSize = 30;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontStyle = FontStyle.Bold;

        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    void CreateCompletadoUI()
{
    // Canvas (mismo que en Pausa)
    GameObject canvasGO = new GameObject("Canvas_Completado");
    Canvas canvas = canvasGO.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 100;  // ← copiado de Pausa

    CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(1920, 1080);  // ← copiado de Pausa

    canvasGO.AddComponent<GraphicRaycaster>();

    // =========================
    // 🔳 FONDO OSCURO (0.8 en lugar de 0.6)
    // =========================
    GameObject bgGO = new GameObject("Background");
    bgGO.transform.SetParent(canvasGO.transform, false);

    Image bgImage = bgGO.AddComponent<Image>();
    bgImage.color = new Color(0f, 0f, 0f, 0.8f);  // ← más oscuro

    RectTransform bgRect = bgGO.GetComponent<RectTransform>();
    bgRect.anchorMin = Vector2.zero;
    bgRect.anchorMax = Vector2.one;
    bgRect.offsetMin = Vector2.zero;
    bgRect.offsetMax = Vector2.zero;

    // =========================
    // 📝 TEXTO (igual que en Pausa: tamaño 80, negrita, sombra)
    // =========================
    GameObject textGO = new GameObject("Texto_Completado");
    textGO.transform.SetParent(canvasGO.transform, false);

    completadoText = textGO.AddComponent<Text>();
    completadoText.text = "Nivel Completado!";
    completadoText.fontSize = 40;                     // ← igual que Pausa
    completadoText.alignment = TextAnchor.MiddleCenter;
    completadoText.color = Color.white;
    completadoText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
    completadoText.fontStyle = FontStyle.Bold;        // ← negrita

    // Sombra (igual que en Pausa)
    Shadow shadow = textGO.AddComponent<Shadow>();
    shadow.effectColor = Color.black;
    shadow.effectDistance = new Vector2(2, -2);

    RectTransform textRect = textGO.GetComponent<RectTransform>();
    textRect.anchorMin = new Vector2(0.5f, 0.6f);     // ← misma posición
    textRect.anchorMax = new Vector2(0.5f, 0.6f);
    textRect.sizeDelta = new Vector2(800, 150);       // ← más grande
    textRect.anchoredPosition = Vector2.zero;

    // =========================
    // 🔘 BOTONES (usan la misma función CreateButton que en Pausa)
    // =========================
    // La función CreateButton ya define la fuente, tamaño y color de los botones.
    // Para que sean idénticos a los de Pausa, no cambiamos nada aquí.
    CreateButton(canvasGO.transform, "Continuar", new Vector2(0, -50), NextLevel);
    CreateButton(canvasGO.transform, "Reintentar", new Vector2(0, -150), RestartLevel);

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
        string sceneName = SceneManager.GetActiveScene().name;
        int currentLevel = ExtractLevelNumber(sceneName);
        LevelProgress.CompleteLevel(currentLevel);
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
        // Obtener el número del nivel actual
        string currentScene = Application.loadedLevelName;
        int currentLevelNumber = ExtractLevelNumber(currentScene);

        // Calcular el siguiente nivel
        int nextLevelNumber = currentLevelNumber + 1;
        string nextSceneName = "level" + nextLevelNumber;

        // Restaurar timeScale
        Time.timeScale = 1f;

        // Verificar si existe el siguiente nivel
        // Nota: Necesitas tener una lista de niveles o verificar si la escena existe
        if (Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            // Cargar el siguiente nivel
            Application.LoadLevel(nextSceneName);
        }
        else
        {
            // Si no hay más niveles, ir al selector o menu principal
            Debug.Log("¡Juego completado! No hay más niveles.");
            Application.LoadLevel("levelselectorscene"); // O "MainMenu" según prefieras
        }
    }
}
