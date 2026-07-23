using InGame.Context;
using UnityEngine;

namespace InGame.Component.Controller
{
    public abstract class ControllerBase : MonoBehaviour
    {
        protected InputContext InputContext;
        protected ObjectContext ObjectContext;
        public virtual void Init(InputContext inputContext, ObjectContext objectContext)
        {
            InputContext = inputContext;
            ObjectContext = objectContext;
        }

        public virtual void Dispose()
        {
            
        }
    }
}
