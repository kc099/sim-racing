using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Drives a car using four WheelColliders: rear-wheel drive, front-wheel steering, and a gearbox.
// Attach this to the car's root object (the one that has the Rigidbody).
[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider frontLeft;
    public WheelCollider frontRight;
    public WheelCollider rearLeft;
    public WheelCollider rearRight;

    [Header("Wheel Meshes (optional)")]
    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    [Header("Driving")]
    public float motorForce = 1500f;    // torque applied to each rear wheel in first gear
    public float maxSteerAngle = 30f;   // degrees the front wheels turn at full lock
    public float brakeForce = 3000f;    // brake torque applied to every wheel
    public float coastBrake = 100f;     // gentle slowing when you lift off the throttle
    public float downforce = 0f;        // presses the car onto the road more the faster it goes (0 = off)
    public float stickDeadZone = 0.08f; // controller stick movements smaller than this are ignored
    public float steerSpeed = 3f;      // how fast the wheels turn towards the pressed key (higher = snappier)
    public float highSpeedSteerAngle = 8f;   // steering is reduced to this many degrees at fullSteerSpeedKmh
    public float fullSteerSpeedKmh = 120f;   // speed at which steering is at its smallest

    [Header("Steering wheel buttons")]
    public float wheelSensitivity = 2.5f;     // 1 = turn the wheel to its stop for full lock; higher needs less turning
    public int wheelAccelerateButton = 1;     // Cross on a Logitech G29
    public int wheelBrakeButton = 2;          // Square
    public int wheelHandbrakeButton = 3;      // Circle
    public int wheelGearUpButton = 5;         // right paddle
    public int wheelGearDownButton = 6;       // left paddle
    public int wheelAutomaticButton = 10;     // Options: automatic or manual gears
    public bool logWheelButtons = false;      // tick to print each button's number in the Console as you press it

    [Header("Gears")]
    public bool automatic = true;       // press M while playing to switch between automatic and manual
    public float[] gearTopSpeedsKmh = { 45f, 80f, 120f, 160f, 200f };   // top speed of 1st, 2nd, 3rd... gear
    public float reverseTopSpeedKmh = 30f;
    public float idleRpm = 900f;
    public float maxRpm = 7000f;

    [Header("Stability")]
    // Centre of mass relative to the car's pivot. A low value keeps the car from flipping.
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0f);

    // Read by the dashboard and the UI.
    public int CurrentGear { get; private set; } = 1;   // -1 = reverse, 0 = neutral, 1 and up = forward gears
    public float EngineRpm { get; private set; }
    public float SpeedKmh { get; private set; }
    public float SteerInput => steerInput;
    public string GearLabel => CurrentGear == -1 ? "R" : CurrentGear == 0 ? "N" : CurrentGear.ToString();

    private Rigidbody rb;
    private float throttle;       // 0 to 1, pushes the car in the direction of the current gear
    private float steerInput;     // -1 (left) to 1 (right)
    private float footBrake;      // 0 to 1, how hard the brake is pressed
    private bool handBrake;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = centerOfMassOffset;
        EngineRpm = idleRpm;
    }

    // Read the keyboard and the controller every frame. Either can be used at any time.
    private void Update()
    {
        float forwardInput = 0f;   // 0 to 1: W, or how far the right trigger is pulled
        float backInput = 0f;      // 0 to 1: S, or how far the left trigger is pulled
        float steerKeys = 0f;      // -1, 0 or 1 from the keyboard
        float steerStick = 0f;     // -1 to 1 from the controller's stick
        bool shiftUp = false;
        bool shiftDown = false;
        bool toggleAutomatic = false;
        handBrake = false;

        // --- Keyboard: W/S or Up/Down, A/D or Left/Right, Space, Q/E, M ---
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) forwardInput = 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) backInput = 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) steerKeys += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) steerKeys -= 1f;
            handBrake = keyboard.spaceKey.isPressed;
            shiftUp = keyboard.eKey.wasPressedThisFrame;
            shiftDown = keyboard.qKey.wasPressedThisFrame;
            toggleAutomatic = keyboard.mKey.wasPressedThisFrame;
        }

        // --- Gamepad: left stick steers, right trigger accelerates, left trigger brakes,
        //     A = handbrake, RB / LB = gear up / down, X = automatic or manual ---
        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            forwardInput = Mathf.Max(forwardInput, gamepad.rightTrigger.ReadValue());
            backInput = Mathf.Max(backInput, gamepad.leftTrigger.ReadValue());
            steerStick = gamepad.leftStick.x.ReadValue();
            if (Mathf.Abs(steerStick) < stickDeadZone) steerStick = 0f;   // ignore tiny movements around the centre
            handBrake |= gamepad.buttonSouth.isPressed;
            shiftUp |= gamepad.rightShoulder.wasPressedThisFrame;
            shiftDown |= gamepad.leftShoulder.wasPressedThisFrame;
            toggleAutomatic |= gamepad.buttonWest.wasPressedThisFrame;
        }

        // --- Steering wheel (Unity sees it as a joystick): the wheel steers, buttons do the rest.
        //     The pedals are not read, so it works with them unplugged. ---
        Joystick wheel = Joystick.current;
        if (wheel != null)
        {
            // The raw wheel position is -1 at full left lock and 1 at full right lock. Multiplying it
            // means you don't have to turn the wheel all the way round to reach full steering.
            steerStick = Mathf.Clamp(wheel.stick.x.ReadUnprocessedValue() * wheelSensitivity, -1f, 1f);
            if (Mathf.Abs(steerStick) < 0.02f) steerStick = 0f;   // wheel resting at the centre

            if (WheelButton(wheel, wheelAccelerateButton, false)) forwardInput = 1f;
            if (WheelButton(wheel, wheelBrakeButton, false)) backInput = 1f;
            handBrake |= WheelButton(wheel, wheelHandbrakeButton, false);
            shiftUp |= WheelButton(wheel, wheelGearUpButton, true);
            shiftDown |= WheelButton(wheel, wheelGearDownButton, true);
            toggleAutomatic |= WheelButton(wheel, wheelAutomaticButton, true);

            // The plus-shaped direction pad: up accelerates, down brakes.
            if (wheel.hatswitch != null)
            {
                Vector2 pad = wheel.hatswitch.ReadValue();   // y is 1 for up, -1 for down
                if (pad.y > 0.5f) forwardInput = 1f;
                if (pad.y < -0.5f) backInput = 1f;
            }

            if (logWheelButtons) LogPressedWheelButtons(wheel);
        }

        // A key is either up or down, so the keyboard eases the wheels over instead of snapping to
        // full lock. A stick is already smooth, so it is followed much more closely.
        if (steerStick != 0f) steerInput = Mathf.MoveTowards(steerInput, steerStick, steerSpeed * 4f * Time.deltaTime);
        else steerInput = Mathf.MoveTowards(steerInput, steerKeys, steerSpeed * Time.deltaTime);

        if (toggleAutomatic) automatic = !automatic;

        if (automatic)
        {
            ChooseGearAutomatically(forwardInput, backInput);
        }
        else
        {
            // Manual: shift up and down by hand (down from 1st goes to neutral, then reverse).
            if (shiftUp) CurrentGear = Mathf.Min(CurrentGear + 1, gearTopSpeedsKmh.Length);
            if (shiftDown) CurrentGear = Mathf.Max(CurrentGear - 1, -1);
            throttle = forwardInput;
            footBrake = backInput;
        }
    }

    // True if the numbered button on the wheel is held (or, with justPressed, was pressed this frame).
    // Button 1 is called "trigger" by Unity; the rest are "button2", "button3" and so on.
    public static bool WheelButton(Joystick wheel, int number, bool justPressed)
    {
        string controlName = number == 1 ? "trigger" : "button" + number;
        ButtonControl button = wheel.TryGetChildControl<ButtonControl>(controlName);
        if (button == null) return false;
        return justPressed ? button.wasPressedThisFrame : button.isPressed;
    }

    // Prints the number of any wheel button as you press it, to help fill in the button fields.
    private void LogPressedWheelButtons(Joystick wheel)
    {
        for (int number = 1; number <= 32; number++)
        {
            if (WheelButton(wheel, number, true)) Debug.Log("Wheel button " + number + " pressed");
        }
    }

    // Automatic gearbox. Forward drives and back brakes; once the car has stopped, back reverses and forward brakes.
    private void ChooseGearAutomatically(float forwardInput, float backInput)
    {
        bool forwardKey = forwardInput > 0.1f;
        bool backKey = backInput > 0.1f;

        // Speed along the car's nose in metres per second: positive forwards, negative backwards.
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        if (CurrentGear == -1)
        {
            if (forwardKey && forwardSpeed > -1f) CurrentGear = 1;
        }
        else
        {
            if (CurrentGear == 0) CurrentGear = 1;
            if (backKey && !forwardKey && forwardSpeed < 1f) CurrentGear = -1;
        }

        if (CurrentGear == -1)
        {
            throttle = backInput;
            footBrake = forwardInput;
            return;
        }

        throttle = forwardInput;
        footBrake = backInput;

        // Shift up near the top of a gear, and down once the car is well inside the gear below.
        if (CurrentGear < gearTopSpeedsKmh.Length && SpeedKmh > gearTopSpeedsKmh[CurrentGear - 1] * 0.9f)
        {
            CurrentGear++;
        }
        else if (CurrentGear > 1 && SpeedKmh < gearTopSpeedsKmh[CurrentGear - 2] * 0.7f)
        {
            CurrentGear--;
        }
    }

    // Apply forces in step with the physics engine.
    private void FixedUpdate()
    {
        SpeedKmh = rb.linearVelocity.magnitude * 3.6f;

        // Downforce grows with the square of the speed, like a real wing.
        rb.AddForce(-transform.up * (downforce * rb.linearVelocity.sqrMagnitude));

        // --- Engine and gearbox ---
        float motorTorque = 0f;
        float revs = throttle;   // share of the rev range in use, 0 to 1 (in neutral the engine just revs freely)
        if (CurrentGear != 0)
        {
            float gearTopSpeed = CurrentGear == -1 ? reverseTopSpeedKmh : gearTopSpeedsKmh[CurrentGear - 1];
            revs = Mathf.Clamp01(SpeedKmh / gearTopSpeed);

            // Low gears pull hard but run out of speed early; high gears pull gently but go faster.
            float gearStrength = CurrentGear == -1 ? 1f : gearTopSpeedsKmh[0] / gearTopSpeed;

            // Power fades away over the last tenth of the rev range, which limits the speed of each gear.
            float pull = Mathf.InverseLerp(1f, 0.9f, revs);

            float direction = CurrentGear == -1 ? -1f : 1f;
            motorTorque = throttle * motorForce * gearStrength * pull * direction;
        }
        EngineRpm = Mathf.Lerp(EngineRpm, Mathf.Lerp(idleRpm, maxRpm, revs), 10f * Time.fixedDeltaTime);

        // Rear-wheel drive
        rearLeft.motorTorque = motorTorque;
        rearRight.motorTorque = motorTorque;

        // --- Steering (front wheels) ---
        // The faster the car goes, the less the wheels turn, so it doesn't twitch at speed.
        float steerLimit = Mathf.Lerp(maxSteerAngle, highSpeedSteerAngle, SpeedKmh / fullSteerSpeedKmh);
        float steerAngle = steerInput * steerLimit;
        frontLeft.steerAngle = steerAngle;
        frontRight.steerAngle = steerAngle;

        // --- Brakes ---
        // Foot brake works on all four wheels; the handbrake (Space) locks the rear wheels only.
        float frontBrake = footBrake > 0f ? footBrake * brakeForce : (throttle == 0f ? coastBrake : 0f);
        float rearBrake = handBrake ? brakeForce * 2f : frontBrake;
        frontLeft.brakeTorque = frontBrake;
        frontRight.brakeTorque = frontBrake;
        rearLeft.brakeTorque = rearBrake;
        rearRight.brakeTorque = rearBrake;
    }

    // Move the visible wheels after physics has been applied for this frame.
    private void LateUpdate()
    {
        SyncWheelMesh(frontLeft, frontLeftMesh);
        SyncWheelMesh(frontRight, frontRightMesh);
        SyncWheelMesh(rearLeft, rearLeftMesh);
        SyncWheelMesh(rearRight, rearRightMesh);
    }

    // Copies the WheelCollider's position and rotation onto its mesh, if one is assigned.
    private void SyncWheelMesh(WheelCollider wheel, Transform mesh)
    {
        if (mesh == null) return;

        wheel.GetWorldPose(out Vector3 position, out Quaternion rotation);
        mesh.SetPositionAndRotation(position, rotation);
    }
}
