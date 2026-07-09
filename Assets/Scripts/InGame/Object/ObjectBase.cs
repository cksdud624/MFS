using Common;
using Cysharp.Threading.Tasks;
using InGame.Context;
using UniRx;
using UnityEngine;
using ObjectType = Common.GameDefine.ObjectType;
using ObjectState  = Common.GameDefine.ObjectState;

namespace InGame.Object
{
    public class ObjectBase : MonoBehaviour
    {
        public virtual GameDefine.ObjectType ObjectType => ObjectType.Object;
        protected readonly ReactiveProperty<ObjectState> State = new (ObjectState.Raw);
        public ObjectState ObjectState => State.Value;
        
        protected InGameContext InGameContext;

        public async UniTask Init(InGameContext inGameContext)
        {
            InGameContext = inGameContext;
            await UniTask.CompletedTask;
        }
    }
}
