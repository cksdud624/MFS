using InGame.Context;
using UnityEngine;

namespace InGame.Component.Controller
{
    public abstract class ControllerBase : MonoBehaviour
    {
        #region Fields
        protected InGameContext InGameContext;
        protected InputContext InputContext;
        protected ObjectContext ObjectContext;

        //시나리오가 멈춰달라고 한 횟수. 시나리오 재생과 Move가 겹칠 수 있어서 횟수로 센다.
        //켜고 끄기만 하면 시나리오 안에서 Move가 끝나는 순간 시나리오의 멈춤까지 풀려버린다
        private int _suspendCount;

        //시나리오가 이 오브젝트를 강제로 제어하는 중이라 컨트롤러가 입력을 내지 않는지.
        //둘이 같은 InputContext에 쓰면 서로 덮어써서 시나리오가 원하는 대로 움직이지 않는다
        public bool IsSuspended => _suspendCount > 0;
        #endregion

        #region Init
        public virtual void Init(InGameContext inGameContext, InputContext inputContext, ObjectContext objectContext)
        {
            InGameContext = inGameContext;
            InputContext = inputContext;
            ObjectContext = objectContext;
        }
        #endregion

        #region Public
        public virtual void Dispose()
        {
            //컨트롤러가 떨어져 나가면 입력은 비워둔다
            InputContext?.NotifyMove(Vector2.zero);
        }

        /// <summary>입력을 멈춘다. 처음 멈출 때 하던 이동 입력을 비운다. 멈춘 만큼 Resume을 불러야 다시 켜진다</summary>
        public void Suspend()
        {
            if (_suspendCount++ == 0)
                OnSuspended();
        }

        /// <summary>멈춘 것을 하나 푼다. 모두 풀리면 그 사이에 바뀐 입력 상태로 맞추고 다시 켠다</summary>
        public void Resume()
        {
            if (_suspendCount == 0) return;
            if (--_suspendCount == 0)
                OnResumed();
        }
        #endregion

        #region Protected
        protected virtual void OnSuspended() => InputContext.NotifyMove(Vector2.zero);
        protected virtual void OnResumed() { }
        #endregion
    }
}
