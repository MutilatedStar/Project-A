using TMPro;
using UnityEngine;

/// <summary>
/// The hover box for inventory items. Put this on a UI Panel (an Image background) that has a TextMeshPro
/// text as a child, inside the same canvas as the inventory. It stays active but invisible until an item is
/// hovered, then follows the mouse, sizes itself to the text, and stays on screen.
/// </summary>
public class InventoryTooltip : MonoBehaviour
{
    public static InventoryTooltip Instance { get; private set; }

    [Tooltip("The TextMeshPro text inside the panel. Found automatically if left empty.")]
    [SerializeField] private TMP_Text text;
    [SerializeField] private float maxTextWidth = 260f;
    [SerializeField] private float padding = 10f;
    [Tooltip("How far the box sits from the mouse pointer, in screen pixels.")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(18f, 18f);

    private RectTransform rect;
    private RectTransform parentRect;
    private Canvas rootCanvas;
    private CanvasGroup group;
    private bool showing;

    private void Awake()
    {
        Instance = this;

        rect = (RectTransform)transform;
        parentRect = rect.parent as RectTransform;
        Canvas canvas = GetComponentInParent<Canvas>();
        rootCanvas = canvas != null ? canvas.rootCanvas : null;

        // A point anchor keeps sizing and positioning predictable.
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);

        // Hidden with a CanvasGroup (not SetActive) so this script keeps running, and so the box
        // never blocks the mouse.
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        if (text == null) text = GetComponentInChildren<TMP_Text>(true);
        if (text == null) Debug.LogError("InventoryTooltip: needs a TextMeshPro text inside the panel.", this);

        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(string title, string body)
    {
        if (text == null) return;

        text.text = string.IsNullOrEmpty(body) ? "<b>" + title + "</b>" : "<b>" + title + "</b>\n" + body;

        // Size the box to the text.
        Vector2 preferred = text.GetPreferredValues(text.text, maxTextWidth, 0f);
        float textWidth = Mathf.Min(preferred.x + 2f, maxTextWidth);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth + padding * 2f);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferred.y + padding * 2f);

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(padding, padding);
        textRect.offsetMax = new Vector2(-padding, -padding);

        rect.SetAsLastSibling();   // draw on top of the rest of the inventory
        showing = true;
        group.alpha = 1f;
        Follow();
    }

    public void Hide()
    {
        showing = false;
        if (group != null) group.alpha = 0f;
    }

    private void LateUpdate()
    {
        if (showing) Follow();
    }

    // Keeps the box beside the mouse, flipping to the other side near the screen edges.
    private void Follow()
    {
        if (parentRect == null) return;

        Vector2 mouse = Input.mousePosition;
        float scale = rootCanvas != null ? rootCanvas.scaleFactor : 1f;
        float widthPx = rect.rect.width * scale;
        float heightPx = rect.rect.height * scale;

        bool flipX = mouse.x + cursorOffset.x + widthPx > Screen.width;
        bool flipY = mouse.y - cursorOffset.y - heightPx < 0f;

        rect.pivot = new Vector2(flipX ? 1f : 0f, flipY ? 0f : 1f);
        Vector2 screenPoint = mouse + new Vector2(flipX ? -cursorOffset.x : cursorOffset.x,
                                                  flipY ? cursorOffset.y : -cursorOffset.y);

        Camera cam = (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? rootCanvas.worldCamera : null;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parentRect, screenPoint, cam, out Vector3 world))
        {
            rect.position = world;
        }
    }
}
