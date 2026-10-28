using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Notes overlay: a Scroll View + multiline TMP_InputField that sits on top of the notebook
/// instead of inside it. It is shown only while the notebook is open AND the TabController is
/// on the Notes tab, and hidden any other time. Notes load from / save to one text file
/// every time the overlay is shown / hidden.
///
/// Put this script on an object that is ALWAYS active (e.g. an empty "NotesController", or the
/// overlay's Canvas). Do NOT put it on the object it shows and hides (Overlay Root).
/// </summary>
public class NotesMenu : MonoBehaviour
{
    [Header("Hook-up")]
    [Tooltip("Drag the TabController here.")]
    [SerializeField] private TabController tabController;
    [Tooltip("Position of the Notes entry in the TabController's Tabs list (0 = Cover, 1 = Inventory, 2 = Notes...).")]
    [SerializeField] private int notesTabIndex = 2;
    [Tooltip("The object that gets shown/hidden: the overlay holding the Scroll View. It must be a DIFFERENT object from the one this script is on.")]
    [SerializeField] private GameObject overlayRoot;

    [Header("Fields (auto-found inside Overlay Root if left empty)")]
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Settings")]
    [SerializeField] private string fileName = "notes.txt";
    [Tooltip("Extra empty space under the last line so the field always has room to grow.")]
    [SerializeField] private float extraBottomPadding = 80f;
    [Tooltip("How far one notch of the mouse wheel scrolls.")]
    [SerializeField] private float scrollSpeed = 30f;
    [Tooltip("Prints what the notes overlay is doing to the Console. Turn off once it works.")]
    [SerializeField] private bool logEvents = true;

    private RectTransform content;
    private RectTransform inputRect;
    private RectTransform viewport;
    private bool setupOk;         // Awake found everything it needs
    private bool initialized;     // layout setup done (happens the first time the overlay is shown)
    private bool visible;
    private bool loaded;          // never save before a load has happened (would wipe the file)
    private bool needsCaretCheck;
    private int lastCaretPos = -1;

    private string FilePath => Path.Combine(Application.persistentDataPath, fileName);

    private void Log(string message)
    {
        if (logEvents) Debug.Log("[NotesMenu] " + message, this);
    }

    // ---------------------------------------------------------------- setup

    private void Awake()
    {
        // Hide the overlay FIRST, before any checks below. If a check fails and switches this script
        // off, the overlay must not be left stuck on screen. (Skipped if the script is on the overlay
        // itself, since hiding it would switch the script off; the check below reports that mistake.)
        if (overlayRoot != null && overlayRoot != gameObject && !transform.IsChildOf(overlayRoot.transform))
            overlayRoot.SetActive(false);

        if (overlayRoot != null)
        {
            if (scrollRect == null) scrollRect = overlayRoot.GetComponentInChildren<ScrollRect>(true);
            if (inputField == null) inputField = overlayRoot.GetComponentInChildren<TMP_InputField>(true);
        }

        if (tabController == null || overlayRoot == null)
        {
            Debug.LogError("NotesMenu: drag the TabController and the Overlay Root into their fields.", this);
            enabled = false;
            return;
        }

        if (overlayRoot == gameObject || transform.IsChildOf(overlayRoot.transform))
        {
            Debug.LogError("NotesMenu: this script must NOT be on (or inside) the Overlay Root, because hiding " +
                           "the overlay would switch the script off. Put it on an always-active object.", this);
            enabled = false;
            return;
        }

        if (scrollRect == null || inputField == null || scrollRect.content == null)
        {
            Debug.LogError("NotesMenu: Overlay Root needs a ScrollRect (with its Content assigned) and a " +
                           "TMP_InputField inside it.", this);
            enabled = false;
            return;
        }

        // The overlay can only hide what is inside it. If these slots point at objects elsewhere
        // (for example left over from an earlier setup), those objects stay on screen forever.
        if (!inputField.transform.IsChildOf(overlayRoot.transform))
            Debug.LogError("NotesMenu: the Input Field \"" + inputField.name + "\" is NOT inside Overlay Root, " +
                           "so it will never be hidden. Clear the Input Field slot (set it to None) or move the " +
                           "field inside Overlay Root.", inputField);
        if (!scrollRect.transform.IsChildOf(overlayRoot.transform))
            Debug.LogError("NotesMenu: the Scroll Rect \"" + scrollRect.name + "\" is NOT inside Overlay Root, " +
                           "so it will never be hidden. Clear the Scroll Rect slot (set it to None) or move it " +
                           "inside Overlay Root.", scrollRect);

        setupOk = true;
        Log("Ready. Overlay Root = \"" + overlayRoot.name + "\", Notes tab index = " + notesTabIndex + ".");
    }

    // Layout setup. Runs once, the first time the overlay is actually on screen (so sizes are real).
    private bool EnsureInitialized()
    {
        if (initialized) return true;
        if (!setupOk) return false;

        content = scrollRect.content;
        inputRect = inputField.GetComponent<RectTransform>();
        viewport = scrollRect.viewport != null
            ? scrollRect.viewport
            : scrollRect.GetComponent<RectTransform>();

        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = scrollSpeed;

        if (scrollRect.viewport == null)
            Debug.LogWarning("NotesMenu: the ScrollRect has no Viewport assigned (needs a child with " +
                             "a RectMask2D that contains Content). Nothing will be clipped without it.", this);
        if (inputRect == content || !inputRect.IsChildOf(content))
            Debug.LogWarning("NotesMenu: the TMP Input Field should be a CHILD of the ScrollRect's " +
                             "Content, not the Content itself.", this);

        PinToTop(content);
        PinToTop(inputRect);

        inputField.lineType = TMP_InputField.LineType.MultiLineNewline;
        inputField.textComponent.overflowMode = TextOverflowModes.Overflow;
        // Auto Size would rescale the font to fill the (growing) field -> huge text.
        inputField.textComponent.enableAutoSizing = false;
        // Esc closes the notebook (MenuController); by default TMP would also throw away
        // everything typed since the field was clicked when Esc is pressed.
        inputField.restoreOriginalTextOnEscape = false;
        // Tab also toggles the notebook; don't let it get typed into the note as a tab character.
        inputField.onValidateInput += (text, index, ch) => ch == '\t' ? '\0' : ch;
        inputField.onValueChanged.AddListener(OnTextChanged);

        // TMP_InputField swallows wheel events when its text fits inside it, so
        // forward them to the ScrollRect ourselves.
        var forwarder = inputField.gameObject.AddComponent<ScrollForwarder>();
        forwarder.target = scrollRect;

        initialized = true;
        return true;
    }

    // Makes the rect grow downward from its top edge WITHOUT moving it or touching its
    // width/horizontal anchors (so your own layout and margins are left alone).
    private static void PinToTop(RectTransform rt)
    {
        var parent = rt.parent as RectTransform;
        if (parent == null) return;

        var corners = new Vector3[4]; // 0 = bottom-left, 1 = top-left
        rt.GetWorldCorners(corners);
        float topY = parent.InverseTransformPoint(corners[1]).y;
        float bottomY = parent.InverseTransformPoint(corners[0]).y;
        float inset = parent.rect.yMax - topY;
        float height = topY - bottomY;

        rt.pivot = new Vector2(rt.pivot.x, 1f);
        rt.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, inset, height);
    }

    // ------------------------------------------- show / hide, driven by the menu

    private void OnEnable()
    {
        if (!setupOk) return;

        // Always start hidden; the menu tells us when to show.
        overlayRoot.SetActive(false);
        visible = false;

        tabController.TabChanged += OnTabChanged;
        MenuController.OpenChanged += OnOpenChanged;

        // In case we were switched on while the Notes tab was already open.
        if (MenuController.IsOpen && tabController.CurrentTab == notesTabIndex) Show();
    }

    private void OnDisable()
    {
        if (tabController != null) tabController.TabChanged -= OnTabChanged;
        MenuController.OpenChanged -= OnOpenChanged;

        if (visible)
        {
            Save();
            visible = false;
        }
    }

    // Runs right after TabController swaps tabs.
    private void OnTabChanged(int tab)
    {
        Log("Tab changed to " + tab + " (menu open: " + MenuController.IsOpen + ", notes tab: " + notesTabIndex + ").");
        if (MenuController.IsOpen && tab == notesTabIndex) Show();
        else Hide();
    }

    // Runs when the notebook opens or closes (closing doesn't go through a tab swap).
    private void OnOpenChanged(bool open)
    {
        Log("Notebook " + (open ? "opened" : "closed") + ".");
        if (!open) Hide();
    }

    private void Show()
    {
        if (visible) return; // already showing: don't reload over what's been typed

        overlayRoot.SetActive(true);
        Canvas.ForceUpdateCanvases();

        if (!EnsureInitialized())
        {
            overlayRoot.SetActive(false);
            return;
        }

        Load();
        visible = true;
    }

    private void Hide()
    {
        if (!visible) return;

        Save();
        visible = false;

        // Let go of the text field first so nothing is left "typing" into a hidden object.
        EventSystem es = EventSystem.current;
        if (es != null && es.currentSelectedGameObject != null &&
            es.currentSelectedGameObject.transform.IsChildOf(overlayRoot.transform))
        {
            es.SetSelectedGameObject(null);
        }

        overlayRoot.SetActive(false);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && visible) Save();
    }

    public void Save()
    {
        if (!initialized || !loaded || inputField == null) return;
        try
        {
            File.WriteAllText(FilePath, inputField.text);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"NotesMenu: could not save notes: {e.Message}");
        }
    }

    public void Load()
    {
        string text = "";
        bool readOk = true;
        try
        {
            if (File.Exists(FilePath)) text = File.ReadAllText(FilePath);
        }
        catch (System.Exception e)
        {
            readOk = false;
            Debug.LogWarning($"NotesMenu: could not load notes: {e.Message}");
        }

        inputField.SetTextWithoutNotify(text);
        Canvas.ForceUpdateCanvases();
        ResizeToFit();
        content.anchoredPosition = Vector2.zero; // start at the top
        loaded = readOk;
    }

    // ------------------------------------------------------------ auto-sizing

    private void OnTextChanged(string _)
    {
        ResizeToFit();
        needsCaretCheck = true;
    }

    // TMP ignores trailing blank lines when measuring, so a "|" is measured after the text
    // to make every newline (including ones at the very end) add height.
    private float MeasureTextHeight()
    {
        TMP_Text label = inputField.textComponent;
        float width = label.rectTransform.rect.width;
        if (width < 1f) return 0f;
        return label.GetPreferredValues(inputField.text + "|", width, 0f).y;
    }

    private void ResizeToFit()
    {
        float height = Mathf.Max(viewport.rect.height, MeasureTextHeight() + extraBottomPadding);

        inputRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        // The input field scrolls its own text when it thinks the text doesn't fit (it checks
        // before we resize, e.g. on paste). Undo that so only the ScrollRect scrolls.
        inputField.textComponent.rectTransform.anchoredPosition = Vector2.zero;
    }

    // ------------------------------------------------------ caret following

    private void LateUpdate()
    {
        if (!visible || !inputField.isFocused) return;

        if (inputField.caretPosition != lastCaretPos)
        {
            lastCaretPos = inputField.caretPosition;
            needsCaretCheck = true;
        }

        if (needsCaretCheck)
        {
            needsCaretCheck = false;
            KeepCaretVisible();
        }
    }

    private void KeepCaretVisible()
    {
        TMP_Text label = inputField.textComponent;
        Canvas.ForceUpdateCanvases();
        label.ForceMeshUpdate();

        TMP_TextInfo info = label.textInfo;
        if (info == null || info.characterCount == 0 || info.lineCount == 0) return;

        int caret = inputField.caretPosition;
        int index = Mathf.Clamp(caret, 0, info.characterCount - 1);
        TMP_CharacterInfo ch = info.characterInfo[index];
        TMP_LineInfo line = info.lineInfo[Mathf.Clamp(ch.lineNumber, 0, info.lineCount - 1)];

        // Use the whole line's top/bottom so empty lines work too.
        float top = line.ascender;
        float bottom = line.descender;

        // Caret sits after a final newline: it is one line below the last character.
        if (caret >= info.characterCount && ch.character == '\n')
        {
            float h = top - bottom;
            top -= h;
            bottom -= h;
        }

        Transform t = label.transform;
        float topInView = viewport.InverseTransformPoint(t.TransformPoint(new Vector3(0f, top, 0f))).y;
        float bottomInView = viewport.InverseTransformPoint(t.TransformPoint(new Vector3(0f, bottom, 0f))).y;

        Vector2 pos = content.anchoredPosition;
        if (topInView > viewport.rect.yMax) pos.y -= topInView - viewport.rect.yMax;
        else if (bottomInView < viewport.rect.yMin) pos.y += viewport.rect.yMin - bottomInView;
        content.anchoredPosition = pos;
    }

    // --------------------------------------------------------- helper component

    private class ScrollForwarder : MonoBehaviour, IScrollHandler
    {
        public ScrollRect target;

        public void OnScroll(PointerEventData eventData)
        {
            if (target != null) target.OnScroll(eventData);
        }
    }
}
