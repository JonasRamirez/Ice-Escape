using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    private LevelManager levelManagerManager;
    public static int currentLevel = 1;

    void Start()
    {
        LevelProgress.Initialize();
        levelManagerManager = FindObjectOfType<LevelManager>();
        RefreshLevelSelectorButtons();
    }

    public void SendLevelFromButton(Button button)
    {
        string buttonText = button.GetComponentInChildren<Text>().text;
        int levelNumber;

        if (int.TryParse(buttonText, out levelNumber))
        {
            LoadUnlockedLevel(levelNumber, "desde boton con texto: " + buttonText);
        }
        else
        {
            Debug.LogError("El texto del boton no es un numero valido: " + buttonText);
        }
    }

    public void SendLevelFromText(Text buttonText)
    {
        string text = buttonText.text;
        int levelNumber;

        if (int.TryParse(text, out levelNumber))
        {
            LoadUnlockedLevel(levelNumber, "desde texto");
        }
        else
        {
            Debug.LogError("El texto no es un numero valido: " + text);
        }
    }

    public void SendLevelFromString(string levelName)
    {
        int levelNumber = int.Parse(levelName);
        LoadUnlockedLevel(levelNumber, "desde string");
    }

    public void CompleteLevel()
    {
        if (levelManagerManager != null)
        {
            currentLevel = levelManagerManager.ExtractLevelNumber(Application.loadedLevelName);
        }
        else
        {
            int parsedLevel;
            if (int.TryParse(Application.loadedLevelName.Replace("level", ""), out parsedLevel))
            {
                currentLevel = parsedLevel;
            }
        }

        LevelProgress.CompleteLevel(currentLevel);
        Debug.Log("[GameManager] Nivel Completado: " + currentLevel);

        if (currentLevel < LevelProgress.TotalLevels)
        {
            Application.LoadLevel("level" + (currentLevel + 1));
        }
        else
        {
            Application.LoadLevel("mainscene");
        }
    }

    public void RestartLevel()
    {
        Application.LoadLevel("level" + currentLevel);
    }

    public void BackToSelector()
    {
        Application.LoadLevel("levelselectorscene");
    }

    private void LoadUnlockedLevel(int levelNumber, string source)
    {
        if (!LevelProgress.IsLevelUnlocked(levelNumber))
        {
            Debug.Log("Nivel bloqueado: " + levelNumber);
            return;
        }

        currentLevel = levelNumber;
        Application.LoadLevel("level" + levelNumber);
        Debug.Log("Cargando nivel " + levelNumber + " " + source);
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
