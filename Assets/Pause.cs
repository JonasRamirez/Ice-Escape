using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Pause : MonoBehaviour
{
    private GameObject completadoCanvas;
    private Text pausaText;
    private bool isPaused = false;

    void Start()
    {
        CreatePausaUI();
    }

    void Update()
    {
        // Activar/desactivar pausa con Escape o P
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            TogglePause();
        }
    }

    void CreatePausaUI()
    {
        // Canvas
        GameObject canvasGO = new GameObject("Canvas_Pausa");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // Asegurar que esté por encima de todo

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();

        // =========================
        // 🔳 FONDO OSCURO
        // =========================
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);

        Image bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.8f); // Negro más oscuro

        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // =========================
        // 📝 TEXTO "PAUSA"
        // =========================
        GameObject textGO = new GameObject("Texto_Pausa");
        textGO.transform.SetParent(canvasGO.transform, false);

        pausaText = textGO.AddComponent<Text>();
        pausaText.text = "JUEGO PAUSADO";
        pausaText.fontSize = 80;
        pausaText.alignment = TextAnchor.MiddleCenter;
        pausaText.color = Color.white;
        pausaText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        pausaText.fontStyle = FontStyle.Bold;

        // Sombra al texto para mejor visibilidad
        Shadow shadow = textGO.AddComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(2, -2);

        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.6f);
        textRect.anchorMax = new Vector2(0.5f, 0.6f);
        textRect.sizeDelta = new Vector2(800, 150);
        textRect.anchoredPosition = Vector2.zero;

        // =========================
        // 🔘 BOTONES
        // =========================
        CreateButton(canvasGO.transform, "Continuar", new Vector2(0, -50), ResumeGame);
        CreateButton(canvasGO.transform, "Reintentar", new Vector2(0, -150), RestartLevel);
        CreateButton(canvasGO.transform, "Menú Principal", new Vector2(0, -250), GoToMainMenu);

        // Guardamos referencia y ocultamos al inicio
        completadoCanvas = canvasGO;
        completadoCanvas.SetActive(false);
    }

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

    public void TogglePause()
    {
        isPaused = !isPaused;
        
        if (isPaused)
        {
            PauseGame();
            if (completadoCanvas != null)
                completadoCanvas.SetActive(true);
        }
        else
        {
            ResumeGame();
            if (completadoCanvas != null)
                completadoCanvas.SetActive(false);
        }
    }

    void PauseGame()
    {
        Time.timeScale = 0f;
        // Opcional: Desactivar sonidos del juego
        // AudioListener.pause = true;
    }

    void ResumeGame()
    {
        Time.timeScale = 1f;
        isPaused = false;
        // AudioListener.pause = false;
        
        if (completadoCanvas != null)
            completadoCanvas.SetActive(false);
    }

    void RestartLevel()
    {
        Time.timeScale = 1f;
        #if UNITY_5_3_OR_NEWER
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        #else
            Application.LoadLevel(Application.loadedLevelName);
        #endif
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        #if UNITY_5_3_OR_NEWER
            UnityEngine.SceneManagement.SceneManager.LoadScene("mainscene");
        #else
            Application.LoadLevel("mainscene");
        #endif
    }

    // Propiedad para verificar si está pausado desde otros scripts
    public bool IsPaused
    {
        get { return isPaused; }
    }
}