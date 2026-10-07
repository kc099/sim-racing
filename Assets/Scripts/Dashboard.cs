using TMPro;
using UnityEngine;

// On-screen instruments: speedometer needle, rev counter needle, gear display,
// and a steering wheel that turns with the car's steering (shown in driver view only).
public class Dashboard : MonoBehaviour
{
    [Header("Data sources")]
    public CarController car;
    public CameraFollow cameraFollow;

    [Header("Dials")]
    public RectTransform speedNeedle;
    public RectTransform rpmNeedle;
    public float maxSpeedKmh = 240f;       // speed at the end of the speedometer scale
    public float maxDialRpm = 8000f;       // revs at the end of the rev counter scale
    public float needleStartAngle = 135f;  // needle rotation at zero (pointing lower-left)
    public float needleEndAngle = -135f;   // needle rotation at full scale (pointing lower-right)

    [Header("Gear")]
    public TMP_Text gearText;
    public TMP_Text gearModeText;

    [Header("Steering wheel")]
    public RectTransform steeringWheel;
    public float steeringWheelTurn = 110f;  // degrees the on-screen wheel turns at full lock

    private void Update()
    {
        speedNeedle.localRotation = NeedleRotation(car.SpeedKmh / maxSpeedKmh);
        rpmNeedle.localRotation = NeedleRotation(car.EngineRpm / maxDialRpm);

        gearText.text = car.GearLabel;
        gearModeText.text = car.automatic ? "AUTO" : "MANUAL";

        // The wheel is only drawn when you are sitting in the driver's seat.
        steeringWheel.gameObject.SetActive(cameraFollow.IsDriverView);
        steeringWheel.localRotation = Quaternion.Euler(0f, 0f, -car.SteerInput * steeringWheelTurn);
    }

    // share is 0 at the start of the scale and 1 at the end.
    private Quaternion NeedleRotation(float share)
    {
        return Quaternion.Euler(0f, 0f, Mathf.Lerp(needleStartAngle, needleEndAngle, share));
    }
}
