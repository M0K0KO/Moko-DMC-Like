using UnityEngine;
using UnityEngine.InputSystem;

public class InputReader : MonoBehaviour
{
    public Vector2 MoveValue { get; private set; }

    private PlayerInput _actions;
    private bool _attackEdge;
    private bool _jumpEdge;

    private void Awake()
    {
        _actions = new PlayerInput();
    }

    private void OnEnable()
    {
        _actions.Gameplay.Enable();
        _actions.Gameplay.Move.performed += OnMove;
        _actions.Gameplay.Move.canceled += OnMove;
        _actions.Gameplay.Attack.performed += OnAttack;
        _actions.Gameplay.Jump.performed += OnJump;
    }

    private void OnDisable()
    {
        _actions.Gameplay.Move.performed -= OnMove;
        _actions.Gameplay.Move.canceled -= OnMove;
        _actions.Gameplay.Attack.performed -= OnAttack;
        _actions.Gameplay.Jump.performed -= OnJump;
        _actions.Gameplay.Disable();
    }

    private void OnDestroy()
    {
        _actions?.Dispose();
    }

    private void OnAttack(InputAction.CallbackContext _) => _attackEdge = true;
    private void OnMove(InputAction.CallbackContext ctx) => MoveValue = ctx.ReadValue<Vector2>();
    private void OnJump(InputAction.CallbackContext ctx) => _jumpEdge = true;

    public bool ConsumeAttackEdge()
    {
        if (!_attackEdge)
            return false;

        _attackEdge = false;
        return true;
    }

    public bool ConsumeJumpEdge()
    {
        if (!_jumpEdge)
            return false;

        _jumpEdge = false;
        return true;
    }
}