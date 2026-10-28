using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Tracks the player's score and converts it into a letter rank (S -> A -> B -> C -> D -> F).
/// Other scripts call AddScore() when the player does something right (e.g. collects an
/// object) and SubtractScore() when they do something wrong (e.g. a bad investigation choice).
/// Dropping below the F threshold ends the game and loads the failure scene.
///
/// This is a persistent singleton (DontDestroyOnLoad) so an ending scene can read the
/// final rank/score even after the investigation scene has been unloaded.
/// </summary>
public class RankMeter : MonoBehaviour
{
    public static RankMeter Instance { get; private set; }

    public enum Rank { S, A, B, C, D, F }

    [System.Serializable] public class RankEvent : UnityEvent<Rank> { }

    [Header("Score Thresholds (minimum score needed to HOLD this rank)")]
    [Tooltip("Tune these to match how many points your objects/choices actually add up to.")]
    [SerializeField] private int sThreshold = 900;
    [SerializeField] private int aThreshold = 700;
    [SerializeField] private int bThreshold = 500;
    [SerializeField] private int cThreshold = 300;
    [SerializeField] private int dThreshold = 100;
    [Tooltip("Score needed to still be rank F. Anything below this fails the game.")]
    [SerializeField] private int fThreshold = 0;

    [Header("Current State (visible here for debugging in Play mode)")]
    [SerializeField] private int currentScore = 0;
    [SerializeField] private Rank currentRank = Rank.S;

    public int CurrentScore => currentScore;
    public Rank CurrentRank => currentRank;
    public string CurrentRankLetter => currentRank.ToString();

    [Header("Rank Display UI (optional)")]
    [SerializeField] private TMP_Text rankDisplayText;
    [SerializeField] private string rankDisplayFormat = "Rank: {0}";

    [Header("Pickup Notification UI (optional)")]
    [Tooltip("A Text element that pops up to show '<Object Name> +<points>' then hides again.")]
    [SerializeField] private TMP_Text notificationText;
    [SerializeField] private float notificationDuration = 1.5f;
    private Coroutine notificationRoutine;

    [Header("Failure Handling")]
    [SerializeField] private string failureSceneName = "FailureEnding";
    [SerializeField] private bool autoLoadFailureScene = true;

    [Header("Events")]
    public RankEvent onRankChanged;
    public UnityEvent onGameFailed;

    private const string SavedRankKey = "RankMeter_FinalRank";
    private const string SavedScoreKey = "RankMeter_FinalScore";
    private bool hasFailed = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (notificationText != null) notificationText.gameObject.SetActive(false);
        RefreshRank(announce: false);
    }

    /// <summary>Call when the player does something right (e.g. collects an object).</summary>
    public void AddScore(int amount)
    {
        if (hasFailed || amount == 0) return;
        currentScore += Mathf.Abs(amount);
        RefreshRank(announce: true);
    }

    /// <summary>Call to penalize the player. Always pass a positive number.</summary>
    public void SubtractScore(int amount)
    {
        if (hasFailed || amount == 0) return;
        currentScore -= Mathf.Abs(amount);
        RefreshRank(announce: true);
    }

    /// <summary>Shows a brief "Object Name +100" style popup, then hides it again.</summary>
    public void ShowNotification(string message)
    {
        if (notificationText == null) return;
        if (notificationRoutine != null) StopCoroutine(notificationRoutine);
        notificationRoutine = StartCoroutine(NotificationRoutine(message));
    }

    private IEnumerator NotificationRoutine(string message)
    {
        notificationText.text = message;
        notificationText.gameObject.SetActive(true);
        yield return new WaitForSeconds(notificationDuration);
        notificationText.gameObject.SetActive(false);
    }

    private void RefreshRank(bool announce)
    {
        Rank previousRank = currentRank;

        if (currentScore >= sThreshold) currentRank = Rank.S;
        else if (currentScore >= aThreshold) currentRank = Rank.A;
        else if (currentScore >= bThreshold) currentRank = Rank.B;
        else if (currentScore >= cThreshold) currentRank = Rank.C;
        else if (currentScore >= dThreshold) currentRank = Rank.D;
        else if (currentScore >= fThreshold) currentRank = Rank.F;
        else
        {
            currentRank = Rank.F;
            UpdateRankDisplay();
            TriggerFailure();
            return;
        }

        UpdateRankDisplay();

        if (announce && currentRank != previousRank)
            onRankChanged?.Invoke(currentRank);
    }

    private void UpdateRankDisplay()
    {
        if (rankDisplayText != null)
            rankDisplayText.text = string.Format(rankDisplayFormat, currentRank);
    }

    private void TriggerFailure()
    {
        if (hasFailed) return;
        hasFailed = true;

        PlayerPrefs.SetString(SavedRankKey, currentRank.ToString());
        PlayerPrefs.SetInt(SavedScoreKey, currentScore);
        PlayerPrefs.Save();

        onGameFailed?.Invoke();

        if (autoLoadFailureScene && !string.IsNullOrEmpty(failureSceneName))
            SceneManager.LoadScene(failureSceneName);
    }

    /// <summary>Read the saved rank in an ending scene, even if this singleton didn't carry over.</summary>
    public static string GetSavedRankLetter() => PlayerPrefs.GetString(SavedRankKey, Rank.F.ToString());
    public static int GetSavedScore() => PlayerPrefs.GetInt(SavedScoreKey, 0);
}
