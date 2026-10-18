using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// A test driver. It walks the player along a list of points by pressing the real Move and Jump inputs on a virtual
/// gamepad, so the PlayerController and the camera run exactly as they do for a person. It is never in a scene:
/// the editor's LevelRunner adds it while testing and while recording the video.
/// </summary>
public class RouteAutopilot : MonoBehaviour
{
    public bool Finished { get; private set; }
    public bool Stuck { get; private set; }
    public int Index { get { return index; } }

    Vector2[] points;                  // x, z of each point on the route
    bool[] jumpAtPoint;                // press Jump when this point is reached (a take-off point)
    int index;
    bool jumpNow;
    float arriveDistance = 0.5f;
    Gamepad pad;

    float stuckTimer;
    Vector3 stuckFrom;

    public void Setup(Vector2[] route, bool[] jumps)
    {
        points = route;
        jumpAtPoint = jumps;
        index = 0;
    }

    void OnEnable()
    {
        pad = InputSystem.AddDevice<Gamepad>("AutopilotPad");
        stuckFrom = transform.position;
    }

    void OnDisable()
    {
        if (pad != null) InputSystem.RemoveDevice(pad);
    }

    void Update()
    {
        if (pad == null || points == null) return;

        Vector2 stick = Vector2.zero;
        if (!Finished && !Stuck)
        {
            Vector3 flat = new Vector3(points[index].x - transform.position.x, 0f, points[index].y - transform.position.z);
            // the goal's flag pole stands in the middle of the pad, so the last point only needs to be close
            float tolerance = index == points.Length - 1 ? 1.2f : arriveDistance;
            if (flat.magnitude < tolerance)
            {
                jumpNow = jumpAtPoint[index];
                index++;
                if (index >= points.Length) Finished = true;
            }
            else
            {
                // the player moves relative to the camera, so turn the wanted direction into "forward" and "right" of the camera
                Transform cam = Camera.main.transform;
                Vector3 forward = cam.forward; forward.y = 0f; forward.Normalize();
                Vector3 right = cam.right; right.y = 0f; right.Normalize();
                Vector3 want = flat.normalized;
                stick = new Vector2(Vector3.Dot(want, right), Vector3.Dot(want, forward));
            }

            // stuck if it has barely moved in 4 seconds
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 4f)
            {
                if ((transform.position - stuckFrom).sqrMagnitude < 0.25f) Stuck = true;
                stuckFrom = transform.position;
                stuckTimer = 0f;
            }
        }

        uint buttons = jumpNow ? (1u << (int)GamepadButton.South) : 0u;   // South is Jump; held for one frame
        jumpNow = false;
        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = stick, buttons = buttons });
    }
}
