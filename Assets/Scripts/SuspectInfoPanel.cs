using UnityEngine;
using UnityEngine.UI;

public class SuspectInfoPanel : MonoBehaviour
{
    public static SuspectInfoPanel instance;

    [SerializeField] GameObject panel;
    [SerializeField] Text nameText;
    [SerializeField] Text infoText;
    [SerializeField] bool followMouse = true;
    [SerializeField] Vector2 mouseOffset = new Vector2(20, -20);

    void Awake()
    {
        // If this copy has no panel (e.g. it was copied into another scene), switch it off
        // instead of throwing errors. Suspect.cs already handles instance being null.
        if (panel == null)
        {
            Debug.LogWarning("[SuspectInfoPanel] Panel isn't assigned on '" + name + "', so this component is " +
                             "disabled. If this isn't the suspect-select scene, you can delete it here.", this);
            enabled = false;
            return;
        }

        instance = this;

        // The panel must not block the mouse, or hovering would flicker on/off.
        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group == null) group = panel.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        Hide();
    }

    void Update()
    {
        if (followMouse && panel.activeSelf)
            panel.transform.position = (Vector2)Input.mousePosition + mouseOffset;
    }

    public void Show(string suspectName, string info)
    {
        if (panel == null) return;
        if (nameText != null) nameText.text = suspectName;
        if (infoText != null) infoText.text = info;
        panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel == null) return;
        panel.SetActive(false);
    }
}
