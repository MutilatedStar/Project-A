using UnityEngine;
using HisaGames.CutsceneManager;

public class AccusationScene : MonoBehaviour
{
    [Tooltip("Cutscene GameObject names, in suspect order (0, 1, 2).")]
    [SerializeField] string[] suspectCutscenes = new string[3];

    [Tooltip("ON: always play the culprit's cutscene. OFF: play the cutscene of the suspect that was clicked.")]
    [SerializeField] bool playCulpritCutscene = true;

    void Start()
    {
        if (EcCutsceneManager.instance == null)
        {
            Debug.LogWarning("No EcCutsceneManager in this scene.");
            return;
        }

        int index = playCulpritCutscene ? WhoDidIt.Culprit : WhoDidIt.AccusedIndex;
        if (index < 0) index = WhoDidIt.Culprit; // e.g. pressing Play directly in this scene

        if (index >= suspectCutscenes.Length)
        {
            Debug.LogWarning("suspectCutscenes needs an entry for every suspect.");
            return;
        }

        EcCutsceneManager.instance.InitCutscenes(suspectCutscenes[index]);
    }
}