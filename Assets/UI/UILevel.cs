using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UILevel : MonoBehaviour {

    public Text levelText;

    void Start()
    {
        //levelText.text = "Nivel " + LevelManager.currentLevel;
        //Invoke("HideText", 2f);
    }

    void HideText()
    {
        levelText.gameObject.SetActive(false);
    }
}