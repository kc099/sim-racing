using UnityEngine;

// Counts laps and times them. The car must pass every checkpoint in order for a lap to count.
// Attach this to an empty object and fill the checkpoints list in driving order.
public class LapManager : MonoBehaviour
{
    // Element 0 is the finish line, followed by the other checkpoints in driving order.
    public Checkpoint[] checkpoints;

    public int CurrentLap { get; private set; }         // 0 until the car first crosses the finish line
    public float CurrentLapTime { get; private set; }   // seconds since the current lap started
    public float BestLapTime { get; private set; }      // fastest completed lap, 0 if none yet

    private int nextCheckpoint;   // index of the checkpoint the car must reach next

    private void Start()
    {
        // Tell each checkpoint its place in the order and who to report to.
        for (int i = 0; i < checkpoints.Length; i++)
        {
            checkpoints[i].index = i;
            checkpoints[i].lapManager = this;
        }
    }

    private void Update()
    {
        // The clock only runs once the first lap has started.
        if (CurrentLap > 0) CurrentLapTime += Time.deltaTime;
    }

    // Called by a Checkpoint when the car drives through it.
    public void CheckpointReached(int index)
    {
        // Ignore checkpoints hit out of order (or hit twice).
        if (index != nextCheckpoint) return;

        if (index == 0)
        {
            // Crossing the finish line: finish the lap that was running, if any.
            if (CurrentLap > 0)
            {
                if (BestLapTime == 0f || CurrentLapTime < BestLapTime) BestLapTime = CurrentLapTime;
                Debug.Log("Lap " + CurrentLap + " finished in " + CurrentLapTime.ToString("F2") + " s");
            }

            // Start the next lap.
            CurrentLap++;
            CurrentLapTime = 0f;
        }

        nextCheckpoint = (index + 1) % checkpoints.Length;
    }
}
