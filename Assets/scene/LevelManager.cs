using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Importante para acceder a los componentes de UI
using System.Collections;

public class LevelManager : MonoBehaviour
{
    public int currentLevel = 1;

    void Start()
    {
        //Debug.Log("Cargando nivel " + currentLevel);
    }

    // Nueva función que lee el texto del botón que llamó al método
    public void SendLevelFromButton(Button button)
    {
        // Obtener el texto del botón
        string buttonText = button.GetComponentInChildren<Text>().text;

        // Intentar convertir el texto a número
        int levelNumber;

        if (int.TryParse(buttonText, out levelNumber))
        {
            // Si el texto es un número, cargar ese nivel
            Application.LoadLevel("level" + levelNumber);
            Debug.Log("Cargando nivel " + levelNumber + " desde botón con texto: " + buttonText);
        }
        else
        {
            Debug.LogError("El texto del botón no es un número válido: " + buttonText);
        }
    }

    // Versión alternativa: si el botón tiene un componente Text específico
    public void SendLevelFromText(Text buttonText)
    {
        // Obtener el texto
        string text = buttonText.text;

        // Intentar convertir a número
        int levelNumber;

        if (int.TryParse(text, out levelNumber))
        {
            Application.LoadLevel("level" + levelNumber);
            Debug.Log("Cargando nivel " + levelNumber);
        }
        else
        {
            Debug.LogError("El texto no es un número válido: " + text);
        }
    }

    // Versión aún más simple: usar string directamente
    public void SendLevelFromString(string levelName)
    {
        Application.LoadLevel("level" + levelName);
        Debug.Log("Cargando nivel: level" + levelName);
    }

    // Método para completar el nivel
    public void CompleteLevel()
    {
        if (currentLevel < 5)
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

    // Método para volver al selector
    public void BackToSelector()
    {
        Application.LoadLevel("levelselectorscene");
    }
}