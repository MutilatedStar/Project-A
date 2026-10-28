using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
 
// Attach this to an object that is ALWAYS active (for example an empty GameObject named
// "MenuController"), NOT to the menu canvas. Hiding the canvas switches off every script
// on it, which is why Tab could hide the menu but never bring it back.
public class MenuController : MonoBehaviour
{
    public GameObject menuCanvas;
    public TabController tabController;
 
    [Tooltip("ON: the tab hotkeys (I, N, O, Q) only work while the notebook is already open, " +
             "so nobody lands on the wrong tab by accident. " +
             "OFF: pressing one while the notebook is closed opens it straight to that tab.")]
    public bool newbieProtection = true;
 
    // True while the menu is showing. Other scripts (like the player) can read this.
    public static bool IsOpen { get; private set; }
 
    // Raised at the end of SetOpen, with true when the notebook just opened and false when it just closed.
    // (NotesMenu listens to this so the notes overlay hides when the notebook closes.)
    public static event System.Action<bool> OpenChanged;
 
    // True while the player is typing in a text field (like the Notes tab). The letter hotkeys
    // have to stay quiet then, or typing "quiet" would flip tabs.
    public static bool IsTyping
    {
        get
        {
            EventSystem es = EventSystem.current;
            if (es == null || es.currentSelectedGameObject == null) return false;
 
            GameObject selected = es.currentSelectedGameObject;
 
            TMP_InputField tmpField = selected.GetComponent<TMP_InputField>();
            if (tmpField != null && tmpField.isFocused) return true;
 
            InputField legacyField = selected.GetComponent<InputField>();
            return legacyField != null && legacyField.isFocused;
        }
    }
 
    private void Awake()
    {
        if (menuCanvas == null)
        {
            Debug.LogError("MenuController: drag your menu canvas into the Menu Canvas field.", this);
            enabled = false;
            return;
        }
 
        if (tabController == null)
        {
            Debug.LogWarning("MenuController: drag your TabController into the Tab Controller field, or the cover and the tab hotkeys won't work.", this);
        }
 
        if (transform.IsChildOf(menuCanvas.transform))
        {
            Debug.LogWarning("MenuController is on the menu canvas (or inside it), so hiding the canvas also switches this script off. Move it to an object that stays active.", this);
        }
 
        SetOpen(false);
    }
 
    private void Update()
    {
        // Tab and Esc always toggle the notebook. P is a letter, so it's ignored while typing a note.
        if (Input.GetKeyDown(KeyCode.Tab) ||
            Input.GetKeyDown(KeyCode.Escape) ||
            (Input.GetKeyDown(KeyCode.P) && !IsTyping))
        {
            SetOpen(!IsOpen);
            return;
        }
 
        // Notebook closed and Newbie Protection off: a tab hotkey opens the notebook straight to that tab.
        // With protection on, the hotkeys do nothing until the notebook is open.
        if (!IsOpen && !newbieProtection && tabController != null)
        {
            int tab = tabController.GetPressedHotkeyTab();
            if (tab >= 0)
            {
                SetOpen(true);
                tabController.ActivateTab(tab);
            }
        }
    }
 
    // Public so a Resume button's OnClick can call it too.
    public void SetOpen(bool open)
    {
        IsOpen = open;
        menuCanvas.SetActive(open);
 
        // Every time the notebook opens it starts on the cover, which flips to Inventory by itself.
        if (open && tabController != null)
        {
            tabController.ActivateTab(0);
        }
 
        OpenChanged?.Invoke(open);
    }
 
    private void OnDestroy()
    {
        IsOpen = false;
    }
}