using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class SceneManager : MonoBehaviour
{
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
            Application.LoadLevel("Level" + levelNumber);
        }
        else
        {
            Debug.LogError("Nivel no válido: " + levelNumber);
        }
    }
}