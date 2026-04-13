using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    [Header("Level Management")]
    public Text txtLvlDisplay;

    private string currentLevelName;

    void Start()
    {
        LevelProgress.Initialize();
        currentLevelName = SceneManager.GetActiveScene().name;

        if (txtLvlDisplay != null && currentLevelName.StartsWith("level"))
        {
            txtLvlDisplay.text = "Nivel " + ExtractLevelNumber(currentLevelName);
        }

        if (currentLevelName == "levelselectorscene")
        {
            RefreshLevelSelectorButtons();
        }
    }

    public int ExtractLevelNumber(string sceneName)
    {
        string numberPart = sceneName.Replace("level", "");
        int levelNumber = int.Parse(numberPart);
        Debug.Log("[LevelManager] Lvl Number: " + levelNumber);
        return levelNumber;
    }

    public void LoadScene(string sceneName)
    {
        string normalizedSceneName = NormalizeSceneName(sceneName);

        if (!Application.CanStreamedLevelBeLoaded(normalizedSceneName))
        {
            Debug.LogError("La escena no se puede cargar: " + normalizedSceneName);
            return;
        }

        SceneManager.LoadScene(normalizedSceneName);
    }

    public void LoadSceneByIndex(int sceneIndex)
    {
        SceneManager.LoadScene(sceneIndex);
    }

    public void GoToLevelSelector()
    {
        SceneManager.LoadScene("levelselectorscene");
    }

    public void GoToMainMenu()
    {
        SceneManager.LoadScene("mainscene");
    }

    public void LoadLevel(int levelNumber)
    {
        if (levelNumber < 1 || levelNumber > 5)
        {
            Debug.LogError("Nivel no valido: " + levelNumber);
            return;
        }

        if (!LevelProgress.IsLevelUnlocked(levelNumber))
        {
            Debug.Log("Nivel bloqueado: " + levelNumber);
            return;
        }

        string sceneName = "level" + levelNumber;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("La escena no se puede cargar: " + sceneName);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    public void LoadNextLevel()
    {
        if (!currentLevelName.StartsWith("level"))
        {
            GoToMainMenu();
            return;
        }

        int currentLevelNumber = ExtractLevelNumber(currentLevelName);
        if (currentLevelNumber < 5)
        {
            LoadLevel(currentLevelNumber + 1);
        }
        else
        {
            GoToMainMenu();
        }
    }

    private string NormalizeSceneName(string sceneName)
    {
        int levelNumber;
        if (int.TryParse(sceneName, out levelNumber))
        {
            return "level" + levelNumber;
        }

        return sceneName;
    }

    private void RefreshLevelSelectorButtons()
    {
        Button[] buttons = FindObjectsOfType<Button>();

        foreach (Button button in buttons)
        {
            Text buttonText = button.GetComponentInChildren<Text>();
            if (buttonText == null)
            {
                continue;
            }

            int levelNumber;
            if (!int.TryParse(buttonText.text.Trim(), out levelNumber))
            {
                continue;
            }

            LevelProgress.ApplyButtonState(button, levelNumber);
        }
    }
}
