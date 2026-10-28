using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Suspect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Who")]
    [Tooltip("0, 1 or 2. Must be different on each suspect, and match the order in InterrogationManager.")]
    public int suspectIndex;
    public string suspectName;
    [TextArea] public string info;

    [Header("Colors")]
    [Tooltip("Resting color. Keep this slightly darker than white, or the hover fade won't be visible.")]
    [SerializeField] Color normalColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    [SerializeField] Color hoverColor = Color.white;
    [SerializeField] Color clickColor = Color.red;
    [Tooltip("How fast colors fade. Higher = faster.")]
    [SerializeField] float fadeSpeed = 8f;

    [Header("On Click")]
    [Tooltip("OPTIONAL. A UI object (like an 'It's You!' image) that appears when clicked. Leave empty to skip.")]
    [SerializeField] GameObject itsYouGraphic;
    [Tooltip("ON: only the killer shows the graphic. OFF: it shows for everyone.")]
    [SerializeField] bool graphicOnlyForKiller = true;
    [Tooltip("Seconds to show the red/graphic before the scene loads. 0 = load immediately.")]
    [SerializeField] float loadDelay = 1f;

    [Header("Scenes (file names, without .unity)")]
    [Tooltip("Loaded when any suspect is clicked. Must be in File > Build Settings > Scenes In Build.")]
    [SerializeField] string accusationSceneName = "Accusation";

    [Header("Debug")]
    [SerializeField] bool debugLogs = true;

    // Backup copy of the clicked suspect. Lives on this class so nothing else can reset it.
    // InterrogationManager reads this if WhoDidIt.AccusedIndex comes back empty.
    public static int LastAccusedIndex = -1;

    SpriteRenderer sprite;
    Graphic tintGraphic;
    Color targetColor;
    static bool locked; // true once any suspect has been clicked

    void Awake()
    {
        locked = false;

        sprite = GetComponentInChildren<SpriteRenderer>();
        tintGraphic = GetComponentInChildren<Graphic>();

        if (sprite == null && tintGraphic == null)
            Debug.LogWarning(name + ": no SpriteRenderer or UI Image found, so it can't change color.", this);

        targetColor = normalColor;
        SetColor(normalColor);

        if (itsYouGraphic != null)
            itsYouGraphic.SetActive(false);
    }

    void Start()
    {
        // Clicks on sprites only work if all of these are in place.
        if (EventSystem.current == null)
            Debug.LogWarning("[Suspect] No EventSystem in the scene, so clicks and hovers will never fire. " +
                             "Add one with GameObject > UI > Event System.", this);

        if (sprite != null && tintGraphic == null)
        {
            if (GetComponentInChildren<Collider2D>() == null)
                Debug.LogWarning(name + ": sprite suspects need a Collider2D (e.g. BoxCollider2D) to receive clicks.", this);

            Camera cam = Camera.main;
            if (cam != null && cam.GetComponent<Physics2DRaycaster>() == null)
                Debug.LogWarning("[Suspect] The main camera has no Physics2DRaycaster. " +
                                 "Add one to the camera so sprites can receive clicks.", cam);
        }
    }

    void Update()
    {
        SetColor(Color.Lerp(CurrentColor, targetColor, fadeSpeed * Time.unscaledDeltaTime));
    }

    Color CurrentColor
    {
        get
        {
            if (sprite != null) return sprite.color;
            if (tintGraphic != null) return tintGraphic.color;
            return Color.white;
        }
    }

    void SetColor(Color c)
    {
        if (sprite != null) sprite.color = c;
        else if (tintGraphic != null) tintGraphic.color = c;
    }

    void Log(string message)
    {
        if (debugLogs) Debug.Log("[Suspect] " + message, this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (locked) return;
        Log("Hover: " + suspectName);

        targetColor = hoverColor;

        if (SuspectInfoPanel.instance != null)
            SuspectInfoPanel.instance.Show(suspectName, info);
        else
            Debug.LogWarning("No SuspectInfoPanel found in the scene (is the script on the Canvas?).", this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (locked) return;

        targetColor = normalColor;

        if (SuspectInfoPanel.instance != null)
            SuspectInfoPanel.instance.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (locked) return;
        StartCoroutine(ClickSequence());
    }

    IEnumerator ClickSequence()
    {
        locked = true;

        bool isKiller = suspectIndex == WhoDidIt.Culprit;
        string targetScene = accusationSceneName; // every suspect leads to Accusation
        Log("Clicked: " + suspectName + " (index " + suspectIndex + ") | killer: " + isKiller + " -> '" + targetScene + "'");

        // Check the scene BEFORE doing anything, so a bad name is reported right away.
        if (string.IsNullOrWhiteSpace(targetScene) || !Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogError("[Suspect] Can't load scene '" + targetScene + "'. Either the name doesn't match " +
                           "the scene file exactly (it's case-sensitive), or the scene isn't added under " +
                           "File > Build Settings > Scenes In Build. Also check this suspect's Inspector, " +
                           "since values saved there override the defaults in the script.", this);
            locked = false;
            yield break;
        }

        WhoDidIt.AccusedIndex = suspectIndex;
        LastAccusedIndex = suspectIndex;
        Log("Saved accused index " + suspectIndex + " (WhoDidIt.AccusedIndex now reads " + WhoDidIt.AccusedIndex + ")");

        // Turn red right away
        targetColor = clickColor;
        SetColor(clickColor);

        if (SuspectInfoPanel.instance != null)
            SuspectInfoPanel.instance.Hide();

        // Optional graphic: skipped if the slot is empty
        if (itsYouGraphic != null && (isKiller || !graphicOnlyForKiller))
            itsYouGraphic.SetActive(true);

        if (loadDelay > 0f)
            yield return new WaitForSecondsRealtime(loadDelay);

        SceneManager.LoadScene(targetScene);
    }
}
