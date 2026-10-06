using UnityEngine;

// A trigger gate the car drives through. Needs a Collider with "Is Trigger" ticked.
// LapManager fills in the index and lapManager fields automatically when the game starts.
public class Checkpoint : MonoBehaviour
{
    [HideInInspector] public int index;              // position in LapManager's list (0 = finish line)
    [HideInInspector] public LapManager lapManager;

    // Called by Unity when another collider enters this trigger.
    private void OnTriggerEnter(Collider other)
    {
        if (lapManager == null) return;

        // Only react to the car (the object whose Rigidbody has a CarController).
        Rigidbody rb = other.attachedRigidbody;
        if (rb != null && rb.GetComponent<CarController>() != null)
        {
            lapManager.CheckpointReached(index);
        }
    }
}
