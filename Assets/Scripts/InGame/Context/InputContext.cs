using UnityEngine;
using System;

namespace InGame.Context
{
    public class InputContext
    {
        public Vector2 MoveDirection { get; private set; }
        public event Action<Vector2> OnMove;
        public void NotifyMove(Vector2 moveDirection)
        {
            MoveDirection = moveDirection;
            OnMove?.Invoke(moveDirection);
        }

        public event Action OnJump;
        public void NotifyJump() => OnJump?.Invoke();

        public event Action OnDash;
        public void NotifyDash() => OnDash?.Invoke();

        public event Action OnAttack;
        public void NotifyAttack() => OnAttack?.Invoke();
    }
}