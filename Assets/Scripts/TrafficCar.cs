using UnityEngine;

// A simple traffic car: drives round a loop of waypoints at a steady speed
// and stops when another car (or the player) is close in front of it.
[RequireComponent(typeof(Rigidbody))]
public class TrafficCar : MonoBehaviour
{
    public Vector3[] waypoints;        // the loop of points to drive round
    public int nextWaypoint;           // which point the car is heading for
    public float cruiseSpeed = 10f;    // metres per second (10 m/s = 36 km/h)
    public float acceleration = 4f;    // how quickly it speeds up, in m/s per second
    public float stopDistance = 9f;    // stop if a car is this close ahead
    public float turnSpeed = 240f;     // degrees per second at cruise speed

    private Rigidbody rb;
    private float speed;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        speed = cruiseSpeed;
    }

    private void FixedUpdate()
    {
        // Head for the next waypoint; once close, switch to the one after it.
        Vector3 toWaypoint = waypoints[nextWaypoint] - rb.position;
        toWaypoint.y = 0f;
        if (toWaypoint.magnitude < 3f)
        {
            nextWaypoint = (nextWaypoint + 1) % waypoints.Length;
            return;
        }

        // Look ahead from the front bumper. Only cars count; cones and walls are ignored.
        Vector3 eye = rb.position + Vector3.up * 0.8f + transform.forward * 2.4f;
        bool blocked = false;
        if (Physics.Raycast(eye, transform.forward, out RaycastHit hit, stopDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            Rigidbody other = hit.rigidbody;
            blocked = other != null && (other.GetComponent<TrafficCar>() != null || other.GetComponent<CarController>() != null);
        }

        // Brake harder than it accelerates.
        float change = blocked ? acceleration * 3f : acceleration;
        speed = Mathf.MoveTowards(speed, blocked ? 0f : cruiseSpeed, change * Time.fixedDeltaTime);

        // Turn towards the waypoint (only while moving) and roll forwards.
        float turn = turnSpeed * (speed / cruiseSpeed) * Time.fixedDeltaTime;
        Quaternion rotation = Quaternion.RotateTowards(rb.rotation, Quaternion.LookRotation(toWaypoint), turn);
        rb.MoveRotation(rotation);
        rb.MovePosition(rb.position + rotation * Vector3.forward * (speed * Time.fixedDeltaTime));
    }
}
