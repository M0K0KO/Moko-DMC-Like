using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.WSA;

public class InputReader : MonoBehaviour
{
    public Vector2 MoveValue { get; private set; }
    public bool LockOnPressed { get; private set; }
    public float LockSwitchDeltaX { get; private set; }

    private PlayerInput _actions;
    private bool _attackEdge;
    private bool _shootEdge;
    private bool _jumpEdge;
    private bool _launcherEdge;
    private bool _dodgeEdge;
    private bool _swapMeleeEdge;
    private bool _swapRangedEdge;

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
        _actions.Gameplay.Shoot.performed += OnShoot;
        _actions.Gameplay.Jump.performed += OnJump;
        _actions.Gameplay.Launcher.performed += OnLauncher;
        _actions.Gameplay.Dodge.performed += OnDodge;
        _actions.Gameplay.LockOn.performed += OnLockOn;
        _actions.Gameplay.SwapMelee.performed += OnSwapMelee;
        _actions.Gameplay.SwapRanged.performed += OnSwapRanged;
    }

    private void OnDisable()
    {
        _actions.Gameplay.Move.performed -= OnMove;
        _actions.Gameplay.Move.canceled -= OnMove;
        _actions.Gameplay.Attack.performed -= OnAttack;
        _actions.Gameplay.Shoot.performed -= OnShoot;
        _actions.Gameplay.Jump.performed -= OnJump;
        _actions.Gameplay.Launcher.performed -= OnLauncher;
        _actions.Gameplay.Dodge.performed -= OnDodge;
        _actions.Gameplay.LockOn.performed -= OnLockOn;
        _actions.Gameplay.SwapMelee.performed -= OnSwapMelee;
        _actions.Gameplay.SwapRanged.performed -= OnSwapRanged;
        _actions.Gameplay.Disable();
    }

    private void OnDestroy()
    {
        _actions?.Dispose();
    }

    private void OnAttack(InputAction.CallbackContext _) => _attackEdge = true;
    private void OnShoot(InputAction.CallbackContext _) => _shootEdge = true;
    private void OnMove(InputAction.CallbackContext ctx) => MoveValue = ctx.ReadValue<Vector2>();
    private void OnJump(InputAction.CallbackContext ctx) => _jumpEdge = true;
    private void OnLauncher(InputAction.CallbackContext ctx) => _launcherEdge = true;
    private void OnDodge(InputAction.CallbackContext ctx) => _dodgeEdge = true;
    private void OnLockOn(InputAction.CallbackContext ctx) => LockOnPressed = true;
    private void OnSwapMelee(InputAction.CallbackContext ctx) => _swapMeleeEdge = true;
    private void OnSwapRanged(InputAction.CallbackContext ctx) => _swapRangedEdge = true;

    public void AddLockSwitchDeltaX(float delta) => LockSwitchDeltaX += delta;

    public bool ConsumeAttackEdge()
    {
        if (!_attackEdge)
            return false;

        _attackEdge = false;
        return true;
    }

    public bool ConsumeShootEdge()
    {
        if (!_shootEdge)
            return false;

        _shootEdge = false;
        return true;
    }

    public bool ConsumeJumpEdge()
    {
        if (!_jumpEdge)
            return false;

        _jumpEdge = false;
        return true;
    }

    public bool ConsumeLauncherEdge()
    {
        if (!_launcherEdge)
            return false;

        _launcherEdge = false;
        return true;
    }

    public bool ConsumeDodgeEdge()
    {
        if (!_dodgeEdge)
            return false;

        _dodgeEdge = false;
        return true;
    }

    public bool ConsumeLockOn()
    {
        bool p = LockOnPressed;
        LockOnPressed = false;
        return p;
    }

    public float ConsumeLockSwitch() 
    {
        var v = LockSwitchDeltaX; 
        LockSwitchDeltaX = 0f; 
        return v; 
    }
    public bool ConsumeSwapMeleeEdge() 
    {
        if (!_swapMeleeEdge)
            return false;

        _swapMeleeEdge = false;
        return true;
    }
    public bool ConsumeSwapRangedEdge() 
    {
        if (!_swapRangedEdge)
            return false;

        _swapRangedEdge = false;
        return true;
    }
}