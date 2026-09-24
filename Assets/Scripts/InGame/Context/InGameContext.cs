using System.Collections.Generic;
using InGame.Object;
using UnityEngine;

namespace InGame.Context
{
    public class InGameContext
    {
        public ObjectBase Player { get; private set; }
        public void SetPlayer(ObjectBase player)
        {
            Player = player;
        }

        //피격 판정 콜라이더와 그 판정을 가진 오브젝트.
        //공격 판정은 여기 등록된 콜라이더만 맞은 것으로 보고, 주인을 찾아 진영을 가린다.
        //공격 판정도 같은 HitBox 레이어에 있으므로 레이어만으로는 둘을 구분할 수 없다
        private readonly Dictionary<Collider2D, ObjectContext> _hurtBoxes = new();
        public void RegisterHurtBox(Collider2D collider, ObjectContext owner) => _hurtBoxes[collider] = owner;
        public void UnregisterHurtBox(Collider2D collider) => _hurtBoxes.Remove(collider);
        public bool TryGetHurtBoxOwner(Collider2D collider, out ObjectContext owner)
            => _hurtBoxes.TryGetValue(collider, out owner);
    }
}
