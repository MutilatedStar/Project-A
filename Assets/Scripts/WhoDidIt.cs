using UnityEngine;

public static class WhoDidIt
{
    public const int SuspectCount = 3;

    // Set true (before the first Culprit call) if the culprit should be saved between launches.
    public static bool persistAcrossSessions = false;

    const string PrefKey = "WhoDidItCulprit";
    static int culprit = -1;

    /// <summary>Index of whoever the player clicked on (-1 = nobody yet).</summary>
    public static int AccusedIndex = -1;

    /// <summary>Picked at random the first time it's read, then always the same.</summary>
    public static int Culprit
    {
        get
        {
            if (culprit >= 0) return culprit;

            if (persistAcrossSessions && PlayerPrefs.HasKey(PrefKey))
            {
                int saved = PlayerPrefs.GetInt(PrefKey);
                if (saved >= 0 && saved < SuspectCount)
                    return culprit = saved;
            }

            culprit = Random.Range(0, SuspectCount);

            if (persistAcrossSessions)
            {
                PlayerPrefs.SetInt(PrefKey, culprit);
                PlayerPrefs.Save();
            }
            return culprit;
        }
    }

    /// <summary>New game: forget the culprit and the accusation.</summary>
    public static void Reset()
    {
        culprit = -1;
        AccusedIndex = -1;
        PlayerPrefs.DeleteKey(PrefKey);
    }
}