using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Generic tab overlay: shows one object (Overlay Root) on top of the notebook while the notebook
/// is open AND the TabController is on this script's tab, and hides it any other time.
/// This is NotesMenu's show/hide idea without the note saving, so it works for Inventory, Options
/// and Quit. Add one TabOverlay per tab.
///
/// Overlay Root is an empty object that holds everything for that tab, so you can click, move and
/// group things inside it freely. If it contains a Scroll View, the mouse wheel scrolls it (also
/// over an input field inside it) and it jumps back to the top each time the tab opens.
///
/// Sound (optional): while the overlay is showing, clicking any interactable Button / Toggle / Slider /
/// Dropdown / Input Field inside it plays the click sound, and moving the mouse onto one plays the
/// hover sound. Things created while the game runs (like inventory slots) are covered too. Both sounds
/// only play while the Sound On/Off setting (GameSettings.SoundOn) is on.
///
/// Put this script on an object that is ALWAYS active (e.g. an empty "TabOverlays" object). Do NOT
/// put it on, or inside, the Overlay Root it shows and hides. Several TabOverlay components can
/// share the same always-active object.
/// </summary>
public class TabOverlay : MonoBehaviour
{
    [Header("Hook-up")]
    [Tooltip("Drag the TabController here.")]
    [SerializeField] private TabController tabController;
    [Tooltip("Position of this overlay's tab in the TabController's Tabs list (0 = Cover, 1 = Inventory, 2 = Notes, 3 = Options, 4 = Quit).")]
    [SerializeField] private int tabIndex = 1;
    [Tooltip("The empty object that gets shown/hidden and holds everything for this tab. It must be a DIFFERENT object from the one this script is on.")]
    [SerializeField] private GameObject overlayRoot;

    [Header("Optional scrolling (auto-found inside Overlay Root if left empty; fine if there is none)")]
    [SerializeField] private ScrollRect scrollRect;

    [Header("Sound (optional)")]
    [Tooltip("The AudioSource that plays the click/hover sounds. Put it on its OWN empty object (not on this one) with a SoundSwitch on it, so the Sound On/Off setting controls it. Every TabOverlay can share the same one.")]
    [SerializeField] private AudioSource soundSource;
    [Tooltip("Played when a clickable thing in this overlay is clicked. Leave empty for no click sound.")]
    [SerializeField] private AudioClip clickClip;
    [Range(0f, 1f)]
    [SerializeField] private float clickVolume = 1f;
    [Tooltip("Played when the mouse moves onto a clickable thing in this overlay. Leave empty for no hover sound.")]
    [SerializeField] private AudioClip hoverClip;
    [Range(0f, 1f)]
    [SerializeField] private float hoverVolume = 0.5f;

    [Header("Settings")]
    [Tooltip("Locks the Scroll View to up/down scrolling (same as Notes). Turn off if you want sideways scrolling too.")]
    [SerializeField] private bool verticalOnly = true;
    [Tooltip("How far one notch of the mouse wheel scrolls.")]
    [SerializeField] private float scrollSpeed = 30f;
    [Tooltip("Jump back to the top of the Scroll View every time this tab opens.")]
    [SerializeField] private bool scrollToTopOnShow = true;
    [Tooltip("Prints what this overlay is doing to the Console. Turn off once it works.")]
    [SerializeField] private bool logEvents = false;

    private bool setupOk;   // Awake found everything it needs
    private bool visible;

    // Sound / hover tracking
    private readonly List<RaycastResult> hits = new List<RaycastResult>();
    private PointerEventData pointerData;
    private EventSystem pointerEventSystem;
    private Selectable hovered;
    private bool hoverPrimed;   // false right after the overlay opens, so opening it never makes a sound by itself

    private void Log(string message)
    {
        if (logEvents) Debug.Log("[TabOverlay " + tabIndex + "] " + message, this);
    }

    // ---------------------------------------------------------------- setup

    private void Awake()
    {
        // Hide the overlay FIRST, before any checks below, so a failed check can't leave it stuck on screen.
        // (Skipped if the script is on the overlay itself, since hiding it would switch the script off;
        // the check below reports that mistake.)
        if (overlayRoot != null && overlayRoot != gameObject && !transform.IsChildOf(overlayRoot.transform))
            overlayRoot.SetActive(false);

        if (tabController == null || overlayRoot == null)
        {
            Debug.LogError("TabOverlay: drag the TabController and the Overlay Root into their fields.", this);
            enabled = false;
            return;
        }

        if (overlayRoot == gameObject || transform.IsChildOf(overlayRoot.transform))
        {
            Debug.LogError("TabOverlay: this script must NOT be on (or inside) the Overlay Root, because hiding " +
                           "the overlay would switch the script off. Put it on an always-active object.", this);
            enabled = false;
            return;
        }

        if (scrollRect == null) scrollRect = overlayRoot.GetComponentInChildren<ScrollRect>(true);
        if (scrollRect != null) SetUpScrolling();

        if (soundSource != null)
        {
            soundSource.playOnAwake = false;
            // The notebook may pause the game while it is open; the UI sounds still have to play.
            soundSource.ignoreListenerPause = true;
        }

        setupOk = true;
        Log("Ready. Overlay Root = \"" + overlayRoot.name + "\", tab index = " + tabIndex + ".");
    }

    private void SetUpScrolling()
    {
        if (!scrollRect.transform.IsChildOf(overlayRoot.transform))
            Debug.LogError("TabOverlay: the Scroll Rect \"" + scrollRect.name + "\" is NOT inside Overlay Root, " +
                           "so it will never be hidden. Clear the Scroll Rect slot (set it to None) or move it " +
                           "inside Overlay Root.", scrollRect);

        if (scrollRect.content == null)
        {
            Debug.LogWarning("TabOverlay: the Scroll Rect \"" + scrollRect.name + "\" has no Content assigned, " +
                             "so scrolling is left alone.", scrollRect);
            scrollRect = null;
            return;
        }

        if (scrollRect.viewport == null)
            Debug.LogWarning("TabOverlay: the Scroll Rect \"" + scrollRect.name + "\" has no Viewport assigned " +
                             "(needs a child with a RectMask2D that contains Content). Nothing will be clipped without it.", scrollRect);

        if (verticalOnly)
        {
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
        }
        scrollRect.scrollSensitivity = scrollSpeed;

        // TMP_InputField swallows wheel events, so forward them to the ScrollRect ourselves.
        foreach (TMP_InputField field in scrollRect.content.GetComponentsInChildren<TMP_InputField>(true))
        {
            if (field.GetComponent<ScrollForwarder>() != null) continue;
            field.gameObject.AddComponent<ScrollForwarder>().target = scrollRect;
        }
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

        // In case we were switched on while this tab was already open.
        if (MenuController.IsOpen && tabController.CurrentTab == tabIndex) Show();
    }

    private void OnDisable()
    {
        if (tabController != null) tabController.TabChanged -= OnTabChanged;
        MenuController.OpenChanged -= OnOpenChanged;

        if (visible && overlayRoot != null) Hide();
    }

    // Runs right after TabController swaps tabs.
    private void OnTabChanged(int tab)
    {
        Log("Tab changed to " + tab + " (menu open: " + MenuController.IsOpen + ").");
        if (MenuController.IsOpen && tab == tabIndex) Show();
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
        if (visible) return;

        overlayRoot.SetActive(true);
        visible = true;
        hovered = null;
        hoverPrimed = false;

        if (scrollRect != null && scrollToTopOnShow)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;
            if (!verticalOnly) scrollRect.horizontalNormalizedPosition = 0f;
        }
    }

    private void Hide()
    {
        if (!visible) return;
        visible = false;
        hovered = null;

        // Let go of whatever is selected inside the overlay so nothing is left "active" on a hidden object.
        EventSystem es = EventSystem.current;
        if (es != null && es.currentSelectedGameObject != null &&
            es.currentSelectedGameObject.transform.IsChildOf(overlayRoot.transform))
        {
            es.SetSelectedGameObject(null);
        }

        overlayRoot.SetActive(false);
    }

    // ------------------------------------------------------- click / hover sounds

    private void LateUpdate()
    {
        if (!visible || soundSource == null) return;
        if (clickClip == null && hoverClip == null) return;

        Selectable under = SelectableUnderPointer();

        // First frame after the overlay opens: just note what the mouse is over, silently.
        if (!hoverPrimed)
        {
            hoverPrimed = true;
            hovered = under;
            return;
        }

        if (under != hovered)
        {
            hovered = under;
            if (under != null) PlaySound(hoverClip, hoverVolume);
        }

        if (under != null && Input.GetMouseButtonDown(0)) PlaySound(clickClip, clickVolume);
    }

    // The interactable thing inside this overlay that the mouse is on top of (and not blocked by something
    // drawn above it), or null. Looked up every frame, so slots created at runtime work without any setup.
    private Selectable SelectableUnderPointer()
    {
        EventSystem es = EventSystem.current;
        if (es == null) return null;

        if (pointerData == null || pointerEventSystem != es)
        {
            pointerData = new PointerEventData(es);
            pointerEventSystem = es;
        }

        pointerData.position = Input.mousePosition;
        hits.Clear();
        es.RaycastAll(pointerData, hits);
        if (hits.Count == 0) return null;

        // The list is sorted front to back, so the first hit is what the mouse is really on.
        Selectable selectable = hits[0].gameObject.GetComponentInParent<Selectable>();
        if (selectable == null) return null;
        if (selectable is Scrollbar) return null;                       // dragging the scroll bar stays silent
        if (!selectable.IsInteractable()) return null;                  // greyed-out things stay silent
        if (!selectable.transform.IsChildOf(overlayRoot.transform)) return null;
        return selectable;
    }

    // Respects the Sound On/Off menu: the SoundSwitch on the AudioSource's object handles the source itself,
    // and the SoundOn check here keeps it silent even if that source is still switched on.
    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null || soundSource == null) return;
        if (!GameSettings.SoundOn) return;
        if (!soundSource.isActiveAndEnabled) return;   // SoundSwitch turned it off

        soundSource.PlayOneShot(clip, volume);
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
