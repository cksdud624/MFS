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
        #endregion
    }
}
