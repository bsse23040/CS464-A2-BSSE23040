using UnityEngine;

/// <summary>
/// Follows the player from a fixed offset. It moves in LateUpdate, after the player has moved, and the smoothing
/// gives the same result at any frame rate. It aims a little ahead of where the player is going.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;                           // left empty in the prefab: found in Awake
    [SerializeField] Vector3 offset = new Vector3(0f, 7.5f, -8f);
    [SerializeField] float sharpness = 5f;                       // k in the smoothing: higher follows tighter
    [SerializeField] float lookAhead = 0.35f;                    // seconds: aim at where the player will be
    [SerializeField] float aimHeight = 1f;                       // look at the player's middle, not the feet

    Rigidbody targetBody;

    void Awake()
    {
        if (target == null)
        {
            var player = FindAnyObjectByType<PlayerController>();
            if (player != null) target = player.transform;
        }
        if (target != null) targetBody = target.GetComponent<Rigidbody>();
    }

    void Start()
    {
        if (target != null) transform.position = AimPoint() + offset;   // start in place, no flight from the origin
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 aim = AimPoint();
        Vector3 wanted = aim + offset;

        // exact smoothing: blend = 1 - e^(-k dt). The camera covers the same share of the gap per second at 15, 60 or 144 fps.
        float blend = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, wanted, blend);

        // look at the point we aim at, not at the player, or the look-ahead is undone
        transform.rotation = Quaternion.LookRotation(aim + Vector3.up * aimHeight - transform.position, Vector3.up);
    }

    // the player's position plus where they will be in lookAhead seconds, flat on the ground
    Vector3 AimPoint()
    {
        Vector3 velocity = targetBody != null ? targetBody.linearVelocity : Vector3.zero;
        velocity.y = 0f;
        return target.position + velocity * lookAhead;
    }
}
