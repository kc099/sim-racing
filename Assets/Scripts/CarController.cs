using UnityEngine;
using UnityEngine.InputSystem;

// Drives a car using four WheelColliders: rear-wheel drive, front-wheel steering.
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
    public float motorForce = 1500f;    // torque applied to each rear wheel
    public float maxSteerAngle = 30f;   // degrees the front wheels turn at full lock
    public float brakeForce = 3000f;    // brake torque applied to every wheel
    public float steerSpeed = 3f;       // how fast the wheels turn towards the pressed key (higher = snappier)
    public float highSpeedSteerAngle = 8f;   // steering is reduced to this many degrees at fullSteerSpeedKmh
    public float fullSteerSpeedKmh = 120f;   // speed at which steering is at its smallest

    [Header("Stability")]
    // Centre of mass relative to the car's pivot. A low value keeps the car from flipping.
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0f);

    private Rigidbody rb;
    private float throttleInput;  // -1 (reverse) to 1 (forward)
    private float steerInput;     // -1 (left) to 1 (right)
    private bool isBraking;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = centerOfMassOffset;
    }

    // Read the keyboard every frame.
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // W / Up = forward, S / Down = reverse
        throttleInput = 0f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) throttleInput += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) throttleInput -= 1f;

        // A / Left = steer left, D / Right = steer right
        float steerTarget = 0f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) steerTarget += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) steerTarget -= 1f;

        // A key is either up or down, so ease the wheels towards it instead of snapping to full lock.
        steerInput = Mathf.MoveTowards(steerInput, steerTarget, steerSpeed * Time.deltaTime);

        isBraking = keyboard.spaceKey.isPressed;
    }

    // Apply forces in step with the physics engine.
    private void FixedUpdate()
    {
        // Rear-wheel drive
        rearLeft.motorTorque = throttleInput * motorForce;
        rearRight.motorTorque = throttleInput * motorForce;

        // Front-wheel steering
        // The faster the car goes, the less the wheels turn, so it doesn't twitch at speed.
        float speedKmh = rb.linearVelocity.magnitude * 3.6f;
        float steerLimit = Mathf.Lerp(maxSteerAngle, highSpeedSteerAngle, speedKmh / fullSteerSpeedKmh);
        float steerAngle = steerInput * steerLimit;
        frontLeft.steerAngle = steerAngle;
        frontRight.steerAngle = steerAngle;

        // Brakes on all four wheels while Space is held
        float brakeTorque = isBraking ? brakeForce : 0f;
        frontLeft.brakeTorque = brakeTorque;
        frontRight.brakeTorque = brakeTorque;
        rearLeft.brakeTorque = brakeTorque;
        rearRight.brakeTorque = brakeTorque;
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
