using UnityEngine;
using UnityEngine.InputSystem;

// Управление танком игрока: WASD/стрелки = движение, мышь = прицел, ЛКМ/пробел = выстрел.
// Корневой объект игрока НЕ вращается (поворачиваются только дочерние Body и Turret).
[RequireComponent(typeof(Rigidbody2D), typeof(TankGun))]
public class PlayerTank : MonoBehaviour
{
    [SerializeField] float moveSpeed = 4f;
    [SerializeField] Transform body;               // спрайт корпуса, поворачивается по направлению движения
    [SerializeField] float bodyTurnSpeed = 720f;   // градусов в секунду

    public static PlayerTank Instance { get; private set; } // чтобы враги легко нашли игрока
    public bool IsMoving => moveInput.sqrMagnitude > 0.01f;
    public TankGun Gun => gun;

    Rigidbody2D rb;
    TankGun gun;
    Camera cam;
    Vector2 moveInput;

    void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody2D>();
        gun = GetComponent<TankGun>();
        cam = Camera.main;

        // Настройки физики задаём кодом, чтобы не забыть в инспекторе
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        // Движение
        Vector2 input = Vector2.zero;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
        }
        moveInput = input.normalized;

        if (body != null && IsMoving)
            body.rotation = Quaternion.RotateTowards(body.rotation, Ricochet.DirToRotation(moveInput),
                                                     bodyTurnSpeed * Time.deltaTime);

        // Прицел за мышью
        if (mouse != null && cam != null)
        {
            Vector2 mouseWorld = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            gun.AimAtPoint(mouseWorld);
        }

        // Выстрел
        bool fire = (mouse != null && mouse.leftButton.wasPressedThisFrame)
                 || (kb != null && kb.spaceKey.wasPressedThisFrame);
        if (fire) gun.TryFire();
    }

    void FixedUpdate()
    {
        rb.linearVelocity = moveInput * moveSpeed;
    }
}
