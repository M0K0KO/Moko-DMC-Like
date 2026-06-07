using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputReader : MonoBehaviour
{
    public Vector2 MoveValue { get; private set; }

    private PlayerInput _actions;
    private bool _attackEdge;

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
    }

    private void OnDisable()
    {
        _actions.Gameplay.Move.performed -= OnMove;
        _actions.Gameplay.Move.canceled -= OnMove;
        _actions.Gameplay.Attack.performed -= OnAttack;
        _actions.Gameplay.Disable();
    }

    private void OnAttack(InputAction.CallbackContext _) => _attackEdge = true;
    private void OnMove(InputAction.CallbackContext ctx) => MoveValue = ctx.ReadValue<Vector2>();

    public bool ConsumeAttackEdge()
    {
        if (!_attackEdge)
            return false;

        _attackEdge = false;
        return true;
    }
}