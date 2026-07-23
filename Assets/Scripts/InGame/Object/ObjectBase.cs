using Common;
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
        protected CameraController CameraController;
        protected PhysicsController PhysicsController;
        protected ObjectStateController ObjectStateController;

        public bool IsPlayer {get; protected set;}
        public ObjectType ObjectType => ObjectContext?.ObjectType ?? ObjectType.Object;

        public async UniTask Init(InGameContext inGameContext, ObjectData objectData, bool isPlayer = false)
        {
            InGameContext = inGameContext;
            IsPlayer = isPlayer;
            InputContext = new ();
            ObjectContext = new(ObjectType.Object, objectData);
            AnimationPlayer = gameObject.AddComponent<AnimationPlayer>();
            await AnimationPlayer.Init(ObjectContext, objectData.Id);
            CameraController = gameObject.AddComponent<CameraController>();
            await CameraController.Init();
            PhysicsController = gameObject.AddComponent<PhysicsController>();
            await PhysicsController.Init(ObjectContext);
            ObjectStateController = gameObject.AddComponent<ObjectStateController>();
            await ObjectStateController.Init(InputContext, ObjectContext);
        }

        public void AttachController()
        {
            if(Controller != null)
            {
                Debug.LogWarning("Controller is already attached.");
                return;
            }

            if(IsPlayer)
            {
                Controller = gameObject.AddComponent<ControllerPlayer>();
                Controller.Init(InputContext, ObjectContext);
            }
            else
            {
                throw new System.NotImplementedException("AI Controller is not implemented yet.");
            }
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