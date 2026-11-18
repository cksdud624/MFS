using Common;
using Common.Template.Interface;
using InGame.Context;
using UnityEngine;

namespace InGame.Component
{
    /// <summary>
    /// 상태 효과의 남은 시간을 센다. 효과 목록과 계산은 ObjectContext.StatusEffects가 맡고, 여기서는 시간만 흘려보낸다.
    /// ObjectContext는 컴포넌트가 아니라서 스스로 시간을 셀 수 없다.
    /// 전투 수치와 같은 박자로 움직이도록 물리 스텝에서 센다 (공격 판정과 굳는 시간이 물리 스텝을 쓴다)
    /// </summary>
    public class StatusEffectController : MonoBehaviour, IFixedUpdateable
    {
        #region Fields
        private ObjectContext _objectContext;
        #endregion

        #region Init
        public void Init(ObjectContext objectContext)
        {
            _objectContext = objectContext;
            Global.Instance.BindFixedUpdate(this);
        }
        #endregion

        #region LifeCycle
        public void OnFixedUpdate() => _objectContext.StatusEffects.Tick(Time.fixedDeltaTime);

        private void OnDestroy() => Global.Instance?.UnBindFixedUpdate(this);
        #endregion
    }
}
