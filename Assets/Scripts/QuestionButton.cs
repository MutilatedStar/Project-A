using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class QuestionButton : MonoBehaviour
{
    [Header("Scene names (exact, must be in Build Settings)")]
    [SerializeField] string investigationScene = "Investigation";
    [SerializeField] string interrogationScene = "Interrogation";
    [SerializeField] string accusationScene = "Accusation";

    [Header("Button text")]
    [SerializeField] string questionText = "Question";
    [SerializeField] string investigationText = "Investigation";

    [Header("Behavior")]
    [Tooltip("If no suspect has been clicked yet: ON = open Interrogation, OFF = do nothing.")]
    [SerializeField] bool interrogationIfNoSuspect = true;
    [Tooltip("ON: the script hooks itself to the Button. Turn OFF only if you wire QuestionButton > Go() in the Button's OnClick list yourself, or it fires twice.")]
    [SerializeField] bool autoWireClick = true;
    [SerializeField] bool debugLogs = true;

    [Header("Label (auto-found in children if left empty; TMP or legacy Text both work)")]
    [SerializeField] TMP_Text tmpLabel;
    [SerializeField] Text legacyLabel;

    Button button;
    bool insideQuestionScene; // true when sitting in Interrogation or Accusation

    void Awake()
    {
        button = GetComponent<Button>();
        if (tmpLabel == null) tmpLabel = GetComponentInChildren<TMP_Text>(true);
        if (legacyLabel == null) legacyLabel = GetComponentInChildren<Text>(true);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (autoWireClick)
        {
            button.onClick.RemoveListener(Go); // re-hook every time, in case something cleared it
            button.onClick.AddListener(Go);
        }

        Refresh();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Refresh();
    }

    void Log(string message)
    {
        if (debugLogs) Debug.Log("[QuestionButton] " + message, this);
    }

    void Refresh()
    {
        string current = SceneManager.GetActiveScene().name;
        insideQuestionScene = current == interrogationScene || current == accusationScene;
        SetLabel(insideQuestionScene ? investigationText : questionText);
    }

    void SetLabel(string text)
    {
        if (tmpLabel != null) tmpLabel.text = text;
        if (legacyLabel != null) legacyLabel.text = text;
    }

    public void Go()
    {
        string current = SceneManager.GetActiveScene().name;
        Log("Clicked in scene '" + current + "' | insideQuestionScene=" + insideQuestionScene + " | AccusedIndex=" + WhoDidIt.AccusedIndex);

        string target;

        if (insideQuestionScene)
        {
            target = investigationScene;
        }
        else if (WhoDidIt.AccusedIndex >= 0)
        {
            bool isKiller = WhoDidIt.AccusedIndex == WhoDidIt.Culprit;
            target = isKiller ? accusationScene : interrogationScene;
        }
        else if (interrogationIfNoSuspect)
        {
            target = interrogationScene;
        }
        else
        {
            Log("No suspect has been clicked yet, so nothing to do.");
            return;
        }

        Time.timeScale = 1f; // in case the menu paused the game

        if (!Application.CanStreamedLevelBeLoaded(target))
        {
            Debug.LogError("[QuestionButton] Scene '" + target + "' isn't in Build Settings (or the name is misspelled).", this);
            return;
        }

        Log("Loading '" + target + "'");
        SceneManager.LoadScene(target);
    }
}