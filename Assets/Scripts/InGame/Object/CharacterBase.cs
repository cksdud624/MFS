using Cysharp.Threading.Tasks;
using Generated.Table;
using InGame.Component;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;
#if UNITY_EDITOR
using Common.Test;
#endif

namespace InGame.Object
{
    /// <summary>
    /// 캐릭터 전용 확장 지점.
    /// 오브젝트 타입은 ObjectData.IsCharacter로 결정되므로 여기서 따로 지정하지 않는다.
    /// </summary>
    public class CharacterBase : ObjectBase
    {
        #region Fields
        protected HitBoxController HitBoxController;
        protected AttackShapeView AttackShapeView;
        protected StatusEffectController StatusEffectController;
        #endregion

        #region Init
        public override async UniTask Init(InGameContext inGameContext, ObjectData objectData, Team team, bool isPlayer = false)
        {
            //캐릭터는 서로 충돌하지 않아야 하므로 콜라이더가 붙기 전에 레이어부터 맞춰둔다
            if (TryGetLayer(LayerIndependent, out int independentLayer))
                gameObject.layer = independentLayer;

            await base.Init(inGameContext, objectData, team, isPlayer);

            //공격 판정은 캐릭터만 사용한다
            HitBoxController = gameObject.AddComponent<HitBoxController>();
            HitBoxController.Init(InGameContext, ObjectContext);
            //판정을 도형으로 보여준다. 표에 도형을 적어둔 판정(이펙트 그림이 없는 공격)만 보인다
            AttackShapeView = gameObject.AddComponent<AttackShapeView>();
            AttackShapeView.Init(ObjectContext);
            //상태 효과는 전투 수치를 가진 캐릭터에만 걸린다
            StatusEffectController = gameObject.AddComponent<StatusEffectController>();
            StatusEffectController.Init(ObjectContext);

            Debug.Log($"[스테이터스] {name} : Hp {ObjectContext.Hp}/{ObjectContext.MaxHp}, "
                      + $"기계 공격 {ObjectContext.GetAttack(AttackType.Machine)} 방어 {ObjectContext.GetDefense(AttackType.Machine)}, "
                      + $"마법 공격 {ObjectContext.GetAttack(AttackType.Magic)} 방어 {ObjectContext.GetDefense(AttackType.Magic)}");

#if UNITY_EDITOR
            EditorTestKeys.OnToggleStaggerImmune += OnTestToggleStaggerImmune;
            EditorTestKeys.OnAddAttackBuff += OnTestAddAttackBuff;
#endif
        }
        #endregion

        #region LifeCycle
        protected override void OnDestroy()
        {
#if UNITY_EDITOR
            EditorTestKeys.OnToggleStaggerImmune -= OnTestToggleStaggerImmune;
            EditorTestKeys.OnAddAttackBuff -= OnTestAddAttackBuff;
#endif
            base.OnDestroy();
        }
        #endregion

        #region Editor
#if UNITY_EDITOR
        //경직 면역 테스트 키. 싸움에 끼는 캐릭터(플레이어, 적)가 모두 같이 켜고 끈다. 중립(NPC)은 맞지 않으므로 뺀다
        private void OnTestToggleStaggerImmune()
        {
            if (Team == Team.Neutral) return;

            var statusEffects = ObjectContext.StatusEffects;
            if (!statusEffects.Remove(EditorTestKeys.StaggerImmuneEffectId))
                statusEffects.Add(EditorTestKeys.StaggerImmuneEffectId);
        }

        //공격력 증가 테스트 키. 플레이어에게만 건다. 누를 때마다 겹치고 남은 시간이 처음으로 돌아간다
        private void OnTestAddAttackBuff()
        {
            if (!IsPlayer) return;

            ObjectContext.StatusEffects.Add(EditorTestKeys.AttackBuffEffectId);
            Debug.Log($"[테스트 키] {name} 공격력 : 기계 {ObjectContext.GetAttack(AttackType.Machine)}, 마법 {ObjectContext.GetAttack(AttackType.Magic)}");
        }
#endif
        #endregion
    }
}
