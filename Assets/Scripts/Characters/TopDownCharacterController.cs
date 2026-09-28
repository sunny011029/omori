using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public sealed class TopDownCharacterController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 3.5f;
    [SerializeField] private bool useKeyboardInput = true;
    private static readonly int IsWalking = Animator.StringToHash("IsWalking");
    private static readonly int FacingRight = Animator.StringToHash("FacingRight");
    private Rigidbody2D body;
    private Animator animator;
    private Vector2 moveInput;

    public float MoveSpeed { get => moveSpeed; set => moveSpeed = Mathf.Max(0f, value); }
    public bool UseKeyboardInput { get => useKeyboardInput; set { useKeyboardInput = value; SetMoveInput(Vector2.zero); } }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (useKeyboardInput)
            SetMoveInput(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")));
    }

    // Also supports a future joystick or another input provider.
    public void SetMoveInput(Vector2 input)
    {
        moveInput = Vector2.ClampMagnitude(input, 1f);
        if (animator == null) animator = GetComponent<Animator>();
        animator.SetBool(IsWalking, moveInput.sqrMagnitude > 0.0001f);
        // The supplied artwork has left/right walk cycles. Vertical travel retains facing.
        if (Mathf.Abs(moveInput.x) > 0.001f) animator.SetBool(FacingRight, moveInput.x > 0f);
    }

    private void FixedUpdate() => body.velocity = moveInput * moveSpeed;

    private void OnDisable()
    {
        moveInput = Vector2.zero;
        if (body != null) body.velocity = Vector2.zero;
        if (animator != null) animator.SetBool(IsWalking, false);
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) SetMoveInput(Vector2.zero);
    }
}
