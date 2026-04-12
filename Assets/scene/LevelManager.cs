using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.UI; 
using UnityEngine.SceneManagement;


public class LevelManager : MonoBehaviour
{
    [Header("Level Management")]
    public Text txtLvlDisplay;

    private int currentLevelIndex;
    private string currentLevelName;
    
    void Start()
    {
        currentLevelIndex = SceneManager.GetActiveScene().buildIndex;
        currentLevelName = SceneManager.GetActiveScene().name;

        if (txtLvlDisplay != null)
        {
            txtLvlDisplay.text = "Nivel " + ExtractLevelNumber(currentLevelName);
        }
    }

    public int ExtractLevelNumber(string sceneName)
    {
        string numberPart = sceneName.Replace("level", "");
        Debug.Log("[LevelManager] Lvl Number: " + int.Parse(numberPart));
        return int.Parse(numberPart);
    }

    // Método para cargar cualquier escena por nombre
    public void LoadScene(string sceneName)
    {
        Application.LoadLevel(sceneName);
    }

    // Método para cargar por índice (más eficiente)
    public void LoadSceneByIndex(int sceneIndex)
    {
        Application.LoadLevel(sceneIndex);
    }

    // Método específico para ir al selector de niveles
    public void GoToLevelSelector()
    {
        Application.LoadLevel("levelselectorscene");
    }

    // Método para ir al menú principal
    public void GoToMainMenu()
    {
        Application.LoadLevel("mainscene");
    }

    // Método para cargar niveles específicos
    public void LoadLevel(int levelNumber)
    {
        if (levelNumber >= 1 && levelNumber <= 5)
        {
            Application.LoadLevel("level" + levelNumber);
        }
        else
        {
            Debug.LogError("Nivel no válido: " + levelNumber);
        }
    }

    public void LoadNextLevel()
    {
        if (currentLevelIndex < 5)
        {
            Application.LoadLevel("level" + (currentLevelIndex + 1));
        }
        else
        {
            GoToMainMenu();
        }
    }
}