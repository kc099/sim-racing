using TMPro;
using UnityEngine;

// Shows speed, lap count and lap times on screen using TextMeshPro.
// Attach this to the Canvas and fill in the fields below.
public class RaceUI : MonoBehaviour
{
    [Header("Data sources")]
    public Rigidbody carRigidbody;   // the car's Rigidbody, used to read its speed
    public LapManager lapManager;

    [Header("Text elements")]
    public TMP_Text speedText;
    public TMP_Text lapText;
    public TMP_Text lapTimeText;

    private void Update()
    {
        // Rigidbody speed is in metres per second; multiply by 3.6 for km/h.
        float speedKmh = carRigidbody.linearVelocity.magnitude * 3.6f;
        speedText.text = Mathf.RoundToInt(speedKmh) + " km/h";

        lapText.text = "Lap " + lapManager.CurrentLap;

        // Current lap time, with the best lap underneath once one exists.
        string times = "Time " + FormatTime(lapManager.CurrentLapTime);
        if (lapManager.BestLapTime > 0f) times += "\nBest " + FormatTime(lapManager.BestLapTime);
        lapTimeText.text = times;
    }

    // Turns seconds into minutes:seconds.hundredths, e.g. 1:05.37
    private string FormatTime(float seconds)
    {
        int minutes = (int)(seconds / 60f);
        float rest = seconds - minutes * 60f;
        return minutes + ":" + rest.ToString("00.00");
    }
}
