using UnityEngine;
using UnityEngine.InputSystem;

// Camera with three views, switched with the C key:
//   0 = chase (smoothly follows behind the car), 1 = driver (from the seat), 2 = bonnet (over the nose).
// Attach this to the Main Camera and assign the car as the target.
public class CameraFollow : MonoBehaviour
{
    public Transform target;                              // the car to follow

    [Header("Chase view")]
    public Vector3 offset = new Vector3(0f, 3f, -7f);     // camera position relative to the car (up, behind)
    public float followSpeed = 5f;                        // how quickly the camera catches up to the car
    public float rotateSpeed = 5f;                        // how quickly the camera turns to face the car
    public float lookHeight = 1f;                         // aim this far above the car's pivot

    [Header("Driver and bonnet views")]
    public Vector3 driverOffset = new Vector3(0f, 1.15f, -0.1f);   // the driver's eyes, relative to the car
    public Vector3 bonnetOffset = new Vector3(0f, 1.45f, 0.5f);    // just above the nose of the car
    public float lookDownAngle = 4f;                               // tilt these two views down a little
    public int wheelViewButton = 4;                                // steering wheel button that changes view (Triangle on a G29)

    public int View { get; private set; }
    public bool IsDriverView => View == 1;

    // LateUpdate runs after the car has moved for this frame.
    private void LateUpdate()
    {
        if (target == null) return;

        // C on the keyboard, Y on a gamepad or Triangle on a steering wheel switches to the next view.
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        Joystick wheel = Joystick.current;
        bool nextView = (keyboard != null && keyboard.cKey.wasPressedThisFrame)
            || (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame)
            || (wheel != null && CarController.WheelButton(wheel, wheelViewButton, true));
        if (nextView) View = (View + 1) % 3;

        if (View == 0)
        {
            // Move towards the point behind the car (offset is in the car's local space).
            Vector3 desiredPosition = target.TransformPoint(offset);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

            // Turn to look at the car.
            Vector3 lookPoint = target.position + Vector3.up * lookHeight;
            Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotateSpeed * Time.deltaTime);
        }
        else
        {
            // Driver and bonnet views are fixed to the car: they move and turn exactly with it.
            Vector3 seat = View == 1 ? driverOffset : bonnetOffset;
            transform.SetPositionAndRotation(target.TransformPoint(seat), target.rotation * Quaternion.Euler(lookDownAngle, 0f, 0f));
        }
    }
}
