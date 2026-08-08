using UnityEngine;

[RequireComponent(typeof(RCC_CarControllerV4))]
public class CarJumpTracker : MonoBehaviour
{
    private RCC_CarControllerV4 rcc;

    public event System.Action OnLeftGround;
    public event System.Action<float> OnLanded; // airtime in seconds

    private bool wasGrounded = true;
    private float airTime;

    void Awake()
    {
        rcc = GetComponent<RCC_CarControllerV4>();
    }

    void FixedUpdate()
    {
        // rcc.isGrounded is recalculated inside RCC's own FixedUpdate via CheckGrounded(),
        // so reading it here just consumes that result — no duplicate raycasting/wheel checks.
        bool grounded = rcc.isGrounded;

        if (wasGrounded && !grounded)
        {
            airTime = 0f;
            OnLeftGround?.Invoke();
        }
        else if (!wasGrounded && grounded)
        {
            OnLanded?.Invoke(airTime);
        }

        if (!grounded)
            airTime += Time.fixedDeltaTime;

        wasGrounded = grounded;
    }
}