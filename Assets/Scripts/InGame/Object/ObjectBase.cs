using Cysharp.Threading.Tasks;
using Generated.Table;
using InGame.Context;
using UniRx;
using UnityEngine;
using ObjectType = Common.GameDefine.ObjectType;
using ObjectState  = Common.GameDefine.ObjectState;
using InGame.Component;
using InGame.Component.Controller;

namespace InGame.Object
{
    public class ObjectBase : MonoBehaviour
    {
        protected readonly ReactiveProperty<ObjectState> State = new (ObjectState.Raw);
        public ObjectState ObjectState => State.Value;

        protected InGameContext InGameContext;
        protected InputContext InputContext;
        protected ObjectContext ObjectContext;

        protected ControllerBase Controller;
        protected AnimationPlayer AnimationPlayer;
        protected EffectPlayer EffectPlayer;
        protected CameraController CameraController;
        protected PhysicsController PhysicsController;
        protected ObjectStateController ObjectStateController;

        public bool IsPlayer {get; protected set;}
        public ObjectType ObjectType => ObjectContext?.ObjectType ?? ObjectType.Object;

        public virtual async UniTask Init(InGameContext inGameContext, ObjectData objectData, bool isPlayer = false)
        {
            InGameContext = inGameContext;
            IsPlayer = isPlayer;
            InputContext = new ();
            ObjectContext = new(objectData);
            AnimationPlayer = gameObject.AddComponent<AnimationPlayer>();
            await AnimationPlayer.Init(ObjectContext, objectData.Id);
            //이펙트 프리팹은 여기서 미리 로드해두고 재생 요청 때 바로 쓴다
            EffectPlayer = gameObject.AddComponent<EffectPlayer>();
            await EffectPlayer.Init(ObjectContext, objectData.Id);
            //카메라는 플레이어만 따라간다
            if (isPlayer)
            {
                CameraController = gameObject.AddComponent<CameraController>();
                await CameraController.Init();
            }
            PhysicsController = gameObject.AddComponent<PhysicsController>();
            await PhysicsController.Init(ObjectContext);
            ObjectStateController = gameObject.AddComponent<ObjectStateController>();
            await ObjectStateController.Init(InputContext, ObjectContext);
        }

        /// <summary>Order in Layer. 구간이 겹치지 않도록 스포너가 정해서 넘겨준다</summary>
        public void SetSortingOrder(int sortingOrder) => AnimationPlayer.SetSortingOrder(sortingOrder);

        public void AttachController()
        {
            if(Controller != null)
            {
                Debug.LogWarning("Controller is already attached.");
                return;
            }

            Controller = IsPlayer
                ? (ControllerBase)gameObject.AddComponent<ControllerPlayer>()
                : gameObject.AddComponent<ControllerAI>();
            Controller.Init(InGameContext, InputContext, ObjectContext);
        }

        public void DetachController()
        {
            if(Controller == null) return;
            Controller.Dispose();
            Destroy(Controller);
            Controller = null;
        }

        protected virtual void OnDestroy()
        {
            State.Value = ObjectState.Destroyed;
        }
    }
}
