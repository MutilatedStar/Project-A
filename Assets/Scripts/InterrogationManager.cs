using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class InterrogationManager : MonoBehaviour
{
    // ================= DATA (filled in the Inspector) =================

    [System.Serializable]
    public class Variant
    {
        [Tooltip("Text shown on the button.")]
        public string label;
        [Tooltip("OPTIONAL icon for this variant.")]
        public Sprite icon;
    }

    [System.Serializable]
    public class Choice
    {
        [Tooltip("Name used in the score popup, e.g. 'Bloody Knife'.")]
        public string itemName;
        [Tooltip("Different looks for this button (3 recommended). One is picked each time.")]
        public Variant[] variants;
        public bool isCorrect;
        [Tooltip("0 = use the default correct/wrong points.")]
        public int pointsOverride;
        [Tooltip("OPTIONAL: the suspect's reply to THIS item (add variations). Empty = use the round's generic reaction.")]
        [TextArea] public string[] reaction;
    }

    [System.Serializable]
    public class Round
    {
        [Tooltip("What the suspect says before the player chooses (add up to 3 variations).")]
        [TextArea] public string[] promptVariations;
        public Choice[] choices;
        [Tooltip("Generic reactions to a correct choice (variations).")]
        [TextArea] public string[] correctReactions;
        [Tooltip("Generic reactions to a wrong choice (variations).")]
        [TextArea] public string[] wrongReactions;
    }

    [System.Serializable]
    public class SuspectProfile
    {
        public string displayName;
        public Sprite background;
        public Sprite portraitNeutral;
        [Tooltip("OPTIONAL: portrait when the player picks correctly.")]
        public Sprite portraitOnCorrect;
        [Tooltip("OPTIONAL: portrait when the player picks wrongly.")]
        public Sprite portraitOnWrong;
        public Round[] rounds;
    }

    [System.Serializable]
    public class ChoiceSlot
    {
        public Button button;
        public TMP_Text label;
        [Tooltip("A separate child Image (NOT the button's own background image).")]
        public Image icon;
    }

    // ================= INSPECTOR FIELDS =================

    [Header("Suspects (order MUST match suspect index 0, 1, 2)")]
    [SerializeField] SuspectProfile[] suspects = new SuspectProfile[3];

    [Header("Ending")]
    [Tooltip("Exact name of the ending scene (must be in Build Settings).")]
    [SerializeField] string endingSceneName = "Ending";

    [Header("Scene UI (fill what you use; background/portrait can be UI or world sprites)")]
    [SerializeField] Image backgroundImage;
    [SerializeField] SpriteRenderer backgroundSprite;
    [SerializeField] Image portraitImage;
    [SerializeField] SpriteRenderer portraitSprite;
    [SerializeField] Text nameText;
    [SerializeField] Text dialogueText;
    [SerializeField] ChoiceSlot[] choiceSlots;
    [Tooltip("OPTIONAL: an arrow/'click to continue' object shown while waiting.")]
    [SerializeField] GameObject continuePrompt;

    [Header("Scoring (used when a choice has no override)")]
    [SerializeField] int correctPoints = 100;
    [SerializeField] int wrongPenalty = 50;

    [Header("Behavior")]
    [SerializeField] bool shuffleChoices = true;
    [Tooltip("Seconds per character. 0 = show text instantly. Click to skip.")]
    [SerializeField] float typeDelay = 0.03f;
    [Tooltip("Used when you press Play directly in this scene (nobody was clicked).")]
    [SerializeField] int testSuspectIndex = 0;

    [Header("Events")]
    public UnityEvent onInterrogationFinished;

    // ================= RUNTIME =================

    int suspectIndex;
    SuspectProfile profile;
    Choice chosen;

    // Remembers the last variation used so the same one never shows twice in a row,
    // even if this scene is loaded again (resets when the game is relaunched).
    static readonly Dictionary<string, int> lastPicked = new Dictionary<string, int>();

    static int PickVariation(string key, int count)
    {
        if (count <= 1) return 0;
        int last;
        if (!lastPicked.TryGetValue(key, out last)) last = -1;
        int pick;
        do { pick = Random.Range(0, count); } while (pick == last);
        lastPicked[key] = pick;
        return pick;
    }

    static string PickLine(string key, string[] lines)
    {
        if (lines == null || lines.Length == 0) return "";
        return lines[PickVariation(key, lines.Length)];
    }

    static bool AdvancePressed()
    {
        return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
    }

    static void SetSprite(Image image, SpriteRenderer renderer, Sprite sprite)
    {
        if (sprite == null) return;
        if (image != null) image.sprite = sprite;
        if (renderer != null) renderer.sprite = sprite;
    }

    void Start()
    {
        Debug.Log("[Interrogation] WhoDidIt.AccusedIndex = " + WhoDidIt.AccusedIndex +
                  " | Suspect.LastAccusedIndex = " + Suspect.LastAccusedIndex);

        // Try WhoDidIt first, then the backup copy that Suspect saves when clicked.
        suspectIndex = WhoDidIt.AccusedIndex;
        if (suspectIndex < 0 || suspectIndex >= suspects.Length)
            suspectIndex = Suspect.LastAccusedIndex;

        if (suspectIndex < 0 || suspectIndex >= suspects.Length)
        {
            Debug.LogWarning("[Interrogation] No accused suspect yet, using testSuspectIndex " + testSuspectIndex);
            suspectIndex = testSuspectIndex;
        }

        if (suspectIndex < 0 || suspectIndex >= suspects.Length || suspects[suspectIndex] == null)
        {
            Debug.LogError("[Interrogation] No profile set up for suspect " + suspectIndex);
            return;
        }

        profile = suspects[suspectIndex];

        // Self-heal: if Dialogue Text wasn't dragged in, look for a Text object named "dialog", else the biggest non-button text.
        if (dialogueText == null)
        {
            dialogueText = FindDialogueText();
            if (dialogueText != null)
                Debug.LogWarning("[Interrogation] Dialogue Text wasn't assigned, so I guessed '" + dialogueText.name +
                                 "'. If that's wrong, drag the right object into the Inspector slot.", this);
        }

        if (dialogueText == null)
        {
            Debug.LogError("[Interrogation] Dialogue Text isn't assigned and no UI Text object was found in this scene.", this);
            return;
        }

        if (choiceSlots == null || choiceSlots.Length == 0)
        {
            Debug.LogError("[Interrogation] Choice Slots is empty. Add your answer buttons in the Inspector.", this);
            return;
        }

        for (int i = 0; i < choiceSlots.Length; i++)
        {
            if (choiceSlots[i] == null || choiceSlots[i].button == null)
            {
                Debug.LogError("[Interrogation] Choice Slot " + i + " has no Button assigned.", this);
                return;
            }
        }

        if (nameText != null) nameText.text = profile.displayName;
        SetSprite(backgroundImage, backgroundSprite, profile.background);
        SetSprite(portraitImage, portraitSprite, profile.portraitNeutral);
        HideChoices();
        if (continuePrompt != null) continuePrompt.SetActive(false);

        StartCoroutine(RunInterrogation());
    }

    Text FindDialogueText()
    {
        Text[] all = FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Text t in all)
        {
            if (t.gameObject.name.ToLower().Contains("dialog")) return t;
        }

        // Last resort: the biggest text box in the scene that isn't the name or a button label.
        Text best = null;
        float bestArea = 0f;
        foreach (Text t in all)
        {
            if (!t.gameObject.scene.IsValid()) continue;
            if (t == nameText) continue;

            if (t.GetComponentInParent<Button>() != null) continue;

            Rect r = t.rectTransform.rect;
            float area = r.width * r.height;
            if (area > bestArea) { bestArea = area; best = t; }
        }
        return best;
    }

    IEnumerator RunInterrogation()
    {
        if (profile.rounds == null) yield break;

        for (int r = 0; r < profile.rounds.Length; r++)
        {
            Round round = profile.rounds[r];
            if (round.choices == null || round.choices.Length == 0)
            {
                Debug.LogWarning("[Interrogation] Round " + r + " has no choices, skipping.");
                continue;
            }

            string keyBase = suspectIndex + ":" + r;

            // 1. Suspect speaks
            SetSprite(portraitImage, portraitSprite, profile.portraitNeutral);
            yield return ShowLine(PickLine(keyBase + ":prompt", round.promptVariations), false);

            // 2. Player chooses
            chosen = null;
            ShowChoices(round, keyBase);
            while (chosen == null) yield return null;
            HideChoices();

            // 3. Points
            bool correct = chosen.isCorrect;
            ApplyScore(chosen, correct);

            // 4. Suspect reacts
            SetSprite(portraitImage, portraitSprite, correct ? profile.portraitOnCorrect : profile.portraitOnWrong);
            string[] lines = (chosen.reaction != null && chosen.reaction.Length > 0)
                ? chosen.reaction
                : (correct ? round.correctReactions : round.wrongReactions);
            yield return ShowLine(PickLine(keyBase + ":react:" + (correct ? "c" : "w") + chosen.itemName, lines), true);
        }

        Debug.Log("[Interrogation] Finished.");
        onInterrogationFinished?.Invoke();
        GoToEnding();
    }

    IEnumerator ShowLine(string text, bool waitForClick)
    {
        // Legacy UI Text has no "visible characters" setting, so the typewriter
        // effect works by showing a growing slice of the string.
        if (text == null) text = "";
        int total = text.Length;
        dialogueText.text = "";

        if (typeDelay > 0f && total > 0)
        {
            for (int i = 0; i < total; i++)
            {
                dialogueText.text = text.Substring(0, i);
                float t = 0f;
                while (t < typeDelay)
                {
                    if (AdvancePressed()) { i = total; break; } // click = skip typing
                    t += Time.deltaTime;
                    yield return null;
                }
            }
        }

        dialogueText.text = text;
        yield return null; // swallow the skip click

        if (waitForClick)
        {
            if (continuePrompt != null) continuePrompt.SetActive(true);
            while (!AdvancePressed()) yield return null;
            if (continuePrompt != null) continuePrompt.SetActive(false);
            yield return null; // swallow the advance click
        }
    }

    void ShowChoices(Round round, string keyBase)
    {
        int count = Mathf.Min(round.choices.Length, choiceSlots.Length);
        if (round.choices.Length > choiceSlots.Length)
            Debug.LogWarning("[Interrogation] More choices than buttons. Only the first " + count + " are used.");

        // Which choice goes in which button
        List<int> order = new List<int>();
        for (int i = 0; i < count; i++) order.Add(i);
        if (shuffleChoices)
        {
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
            }
        }

        for (int s = 0; s < choiceSlots.Length; s++)
        {
            ChoiceSlot slot = choiceSlots[s];
            slot.button.onClick.RemoveAllListeners();

            if (s >= count)
            {
                slot.button.gameObject.SetActive(false);
                continue;
            }

            int choiceIndex = order[s];
            Choice choice = round.choices[choiceIndex];

            Variant v = null;
            if (choice.variants != null && choice.variants.Length > 0)
                v = choice.variants[PickVariation(keyBase + ":choice" + choiceIndex, choice.variants.Length)];

            if (slot.label != null)
                slot.label.text = (v != null) ? v.label : choice.itemName;

            if (slot.icon != null)
            {
                bool hasIcon = v != null && v.icon != null;
                slot.icon.gameObject.SetActive(hasIcon);
                if (hasIcon) slot.icon.sprite = v.icon;
            }

            Choice captured = choice;
            slot.button.onClick.AddListener(() => chosen = captured);
            slot.button.gameObject.SetActive(true);
        }
    }

    void HideChoices()
    {
        if (choiceSlots == null) return;
        foreach (ChoiceSlot slot in choiceSlots)
        {
            if (slot.button == null) continue;
            slot.button.onClick.RemoveAllListeners();
            slot.button.gameObject.SetActive(false);
        }
    }

    void GoToEnding()
    {
        if (string.IsNullOrEmpty(endingSceneName))
        {
            Debug.LogError("[Interrogation] No ending scene name set.");
            return;
        }

        SceneManager.LoadScene(endingSceneName);
    }

    void ApplyScore(Choice choice, bool correct)
    {
        int points = choice.pointsOverride > 0 ? choice.pointsOverride : (correct ? correctPoints : wrongPenalty);
        string label = string.IsNullOrEmpty(choice.itemName) ? (correct ? "Correct" : "Wrong") : choice.itemName;

        Debug.Log("[Interrogation] " + label + (correct ? " is CORRECT +" : " is WRONG -") + points);

        if (RankMeter.Instance == null)
        {
            Debug.LogWarning("[Interrogation] No RankMeter found, so no points were applied.");
            return;
        }

        if (correct)
        {
            RankMeter.Instance.AddScore(points);
            RankMeter.Instance.ShowNotification(label + " +" + points);
        }
        else
        {
            RankMeter.Instance.ShowNotification(label + " -" + points);
            RankMeter.Instance.SubtractScore(points);
        }
    }
}
