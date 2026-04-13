using UnityEngine;
using UnityEngine.UI;

public static class LevelProgress
{
    public const int TotalLevels = 5;

    private const string LastUnlockedLevelKey = "LastUnlockedLevel";
    private const string ProgressVersionKey = "LevelProgressVersion";
    private const int CurrentProgressVersion = 2;
    private static readonly Color LockedButtonColor = ParseColor("#5C4C58");
    private static readonly Color UnlockedButtonColor = ParseColor("#F8CD71");

    public static void Initialize()
    {
        if (PlayerPrefs.GetInt(ProgressVersionKey, 0) != CurrentProgressVersion)
        {
            ResetProgress();
            PlayerPrefs.SetInt(ProgressVersionKey, CurrentProgressVersion);
        }

        if (PlayerPrefs.GetInt(LastUnlockedLevelKey, 0) < 1)
        {
            PlayerPrefs.SetInt(LastUnlockedLevelKey, 1);
        }

        if (PlayerPrefs.GetInt(GetUnlockedKey(1), 0) == 0)
        {
            PlayerPrefs.SetInt(GetUnlockedKey(1), 1);
        }

        PlayerPrefs.Save();
    }

    public static void ResetProgress()
    {
        PlayerPrefs.SetInt(LastUnlockedLevelKey, 1);

        for (int levelNumber = 1; levelNumber <= TotalLevels; levelNumber++)
        {
            PlayerPrefs.SetInt(GetUnlockedKey(levelNumber), levelNumber == 1 ? 1 : 0);
            PlayerPrefs.SetInt(GetCompletedKey(levelNumber), 0);
        }

        PlayerPrefs.Save();
    }

    public static int GetLastUnlockedLevel()
    {
        Initialize();
        return Mathf.Clamp(PlayerPrefs.GetInt(LastUnlockedLevelKey, 1), 1, TotalLevels);
    }

    public static bool IsLevelUnlocked(int levelNumber)
    {
        if (levelNumber <= 1) return true;
        if (levelNumber > TotalLevels) return false;

        // Solo verificar la key directa, sin el OR con LastUnlockedLevel
        return PlayerPrefs.GetInt(GetUnlockedKey(levelNumber), 0) == 1;
    }

    public static void CompleteLevel(int currentLevel)
    {
        Initialize();

        PlayerPrefs.SetInt(GetCompletedKey(currentLevel), 1);

        int newLastUnlockedLevel = Mathf.Clamp(currentLevel + 1, 1, TotalLevels);
        if (newLastUnlockedLevel > GetLastUnlockedLevel())
        {
            PlayerPrefs.SetInt(LastUnlockedLevelKey, newLastUnlockedLevel);
        }

        for (int levelNumber = 1; levelNumber <= TotalLevels; levelNumber++)
        {
            PlayerPrefs.SetInt(GetUnlockedKey(levelNumber), levelNumber <= GetLastUnlockedLevel() ? 1 : 0);
        }

        PlayerPrefs.SetInt(GetUnlockedKey(newLastUnlockedLevel), 1);
        PlayerPrefs.Save();
    }

    public static void ApplyButtonState(Button button, int levelNumber)
    {
        bool unlocked = IsLevelUnlocked(levelNumber);
        button.interactable = unlocked;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = unlocked ? UnlockedButtonColor : LockedButtonColor;
        }

        Text text = button.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.color = unlocked ? Color.black : Color.white;
        }

        Color baseColor = unlocked ? UnlockedButtonColor : LockedButtonColor;
        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = baseColor;
        colors.pressedColor = baseColor * 0.95f;
        colors.disabledColor = baseColor;
        button.colors = colors;
    }

    private static string GetUnlockedKey(int levelNumber)
    {
        return "Level" + levelNumber + "_Unlocked";
    }

    private static string GetCompletedKey(int levelNumber)
    {
        return "Level" + levelNumber + "_Completed";
    }

    private static Color ParseColor(string htmlColor)
    {
        Color parsedColor;
        if (ColorUtility.TryParseHtmlString(htmlColor, out parsedColor))
        {
            return parsedColor;
        }

        return Color.white;
    }
}
