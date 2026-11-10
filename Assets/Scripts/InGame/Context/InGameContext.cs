using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Object;
using UnityEngine;

namespace InGame.Context
{
    public class InGameContext
    {
        #region Fields
        public ObjectBase Player { get; private set; }

        //피격 판정 콜라이더와 그 판정을 가진 오브젝트.
        //공격 판정은 여기 등록된 콜라이더만 맞은 것으로 보고, 주인을 찾아 진영을 가린다.
        //공격 판정도 같은 HitBox 레이어에 있으므로 레이어만으로는 둘을 구분할 수 없다
        private readonly Dictionary<Collider2D, ObjectContext> _hurtBoxes = new();

        //플레이어가 Interact를 눌렀다. 말 걸기와 대사 넘기기처럼 캐릭터 동작이 아니라 게임 흐름에 대한 요청이라
        //오브젝트마다 있는 InputContext가 아니라 여기로 받는다
        public event Action OnInteract;
        //OnInteract보다 먼저 받는 쪽 (일반 대화). 대사 넘기기나 말 걸기 목표가 이 입력을 받기 전의 상태로 판단해야 한다
        public event Action OnInteractFirst;
        #endregion

        #region Public
        public void SetPlayer(ObjectBase player)
        {
            Player = player;
        }

        public void RegisterHurtBox(Collider2D collider, ObjectContext owner) => _hurtBoxes[collider] = owner;
        public void UnregisterHurtBox(Collider2D collider) => _hurtBoxes.Remove(collider);
        public bool TryGetHurtBoxOwner(Collider2D collider, out ObjectContext owner)
            => _hurtBoxes.TryGetValue(collider, out owner);

        public void NotifyInteract()
        {
            //지금 기다리는 쪽을 먼저 잡아둔다. 일반 대화가 이 입력으로 시작하면서 대사를 기다리기 시작하는데,
            //그 대기까지 이 입력을 받으면 첫 대사가 다 나오기도 전에 넘어간다
            var waiting = OnInteract;
            OnInteractFirst?.Invoke();
            waiting?.Invoke();
        }

        /// <summary>
        /// 다음 Interact 입력을 기다린다. 한 번 누르면 한 번만 끝난다.
        /// 입력 이벤트 안에서 끝나고 바로 다시 기다려도, 새 구독은 지금 알리고 있는 입력을 받지 않으므로 한 입력이 두 번 쓰이지 않는다
        /// </summary>
        public async UniTask WaitInteract(CancellationToken cancellationToken)
        {
            var source = new UniTaskCompletionSource();
            void Complete() => source.TrySetResult();

            OnInteract += Complete;
            try
            {
                await source.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                OnInteract -= Complete;
            }
        }
        #endregion
    }
}
