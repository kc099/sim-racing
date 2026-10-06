using UnityEngine;

// Smooth third-person camera that stays behind the car and looks at it.
// Attach this to the Main Camera and assign the car as the target.
public class CameraFollow : MonoBehaviour
{
    public Transform target;                              // the car to follow
    public Vector3 offset = new Vector3(0f, 3f, -7f);     // camera position relative to the car (up, behind)
    public float followSpeed = 5f;                        // how quickly the camera catches up to the car
    public float rotateSpeed = 5f;                        // how quickly the camera turns to face the car
    public float lookHeight = 1f;                         // aim this far above the car's pivot

    // LateUpdate runs after the car has moved for this frame.
    private void LateUpdate()
    {
        if (target == null) return;

        // Move towards the point behind the car (offset is in the car's local space).
        Vector3 desiredPosition = target.TransformPoint(offset);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

        // Turn to look at the car.
        Vector3 lookPoint = target.position + Vector3.up * lookHeight;
        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotateSpeed * Time.deltaTime);
    }
}
