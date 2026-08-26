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

        //누른 공격 버튼 번호. 커맨드 문자열은 이 번호를 순서대로 이어붙여서 만든다
        public event Action<int> OnAttack;
        public void NotifyAttack(int attackButton) => OnAttack?.Invoke(attackButton);
    }
}