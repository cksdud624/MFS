using Cysharp.Threading.Tasks;
using Generated.Table;
using InGame.Context;
using R3;
using UnityEngine;
using ObjectType = Common.GameDefine.ObjectType;
using ObjectState  = Common.GameDefine.ObjectState;
using Team = Common.GameDefine.Team;
using InGame.Component;
using InGame.Component.Controller;
#if UNITY_EDITOR
using Common.Test;
#endif

namespace InGame.Object
{
    public class ObjectBase : MonoBehaviour
    {
        #region Fields
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
        protected HurtBox HurtBox;

#if UNITY_EDITOR
        //AI 캐릭터 공격 테스트용. 플레이 중 인스펙터에서 체크한 AI만 테스트 키(EditorTestKeys)로 공격한다
        [Header("Test : AI 캐릭터 공격 테스트용")]
        [Tooltip("체크하면 테스트 키(1번)로 이 AI가 공격한다. 플레이어나 컨트롤러가 AI가 아니면 무시된다")]
        [SerializeField] private bool _aiAttackTestTarget;
#endif

        public bool IsPlayer {get; protected set;}
        public ObjectType ObjectType => ObjectContext?.ObjectType ?? ObjectType.Object;
        #endregion

        #region Init
        public virtual async UniTask Init(InGameContext inGameContext, ObjectData objectData, bool isPlayer = false)
        {
            InGameContext = inGameContext;
            IsPlayer = isPlayer;
            InputContext = new ();
            //진영은 지금 플레이어와 나머지 둘뿐이다. 소환수처럼 플레이어 편이 생기면 스포너가 정해서 넘긴다
            ObjectContext = new(objectData, isPlayer ? Team.Player : Team.Enemy);
            //같은 ID가 진영만 달리해서 나올 수 있으므로 하이어라키와 로그에서 구분되게 진영을 붙인다.
            //진영이 정해지는 곳이 여기라 스포너가 아니라 여기서 붙이고, 뒤이은 컴포넌트 Init 로그에도 반영되게 먼저 바꾼다
            name = $"{ObjectContext.Team}_{objectData.Id}";

            //애니메이션과 이펙트는 에셋을 미리 받아두느라 비동기다
            AnimationPlayer = gameObject.AddComponent<AnimationPlayer>();
            await AnimationPlayer.Init(ObjectContext, objectData);
            EffectPlayer = gameObject.AddComponent<EffectPlayer>();
            await EffectPlayer.Init(InGameContext, ObjectContext, objectData);
            //카메라는 플레이어만 따라간다
            if (isPlayer)
            {
                CameraController = gameObject.AddComponent<CameraController>();
                CameraController.Init();
            }
            PhysicsController = gameObject.AddComponent<PhysicsController>();
            PhysicsController.Init(ObjectContext);
            //상태 컨트롤러는 첫 상태에 들어가면서 애니메이션과 이동 속도를 바로 바꾼다.
            //그 요청을 받을 AnimationPlayer와 PhysicsController가 먼저 붙어 있어야 한다
            ObjectStateController = gameObject.AddComponent<ObjectStateController>();
            ObjectStateController.Init(InputContext, ObjectContext);

            //맞는 판정은 캐릭터가 아닌 오브젝트도 가진다. 본체와 레이어가 달라야 하므로 자식으로 둔다.
            //Rigidbody는 PhysicsController가 붙인 본체 것을 쓰므로 그 뒤에 만든다
            var hurtBoxObject = new GameObject("HurtBox");
            hurtBoxObject.transform.SetParent(transform, false);
            HurtBox = hurtBoxObject.AddComponent<HurtBox>();
            HurtBox.Init(InGameContext, ObjectContext);
        }
        #endregion

        #region LifeCycle
        protected virtual void OnDestroy()
        {
#if UNITY_EDITOR
            EditorTestKeys.OnAIAttack -= OnTestAIAttack;
#endif
            State.Value = ObjectState.Destroyed;
            //R3의 ReactiveProperty는 Dispose해야 구독자에게 완료가 전달되고 구독이 정리된다.
            //파괴 상태를 먼저 알린 뒤 닫는다. 순서가 바뀌면 구독자가 Destroyed를 받지 못한다
            State.Dispose();
        }
        #endregion

        #region Public
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
#if UNITY_EDITOR
            if (Controller is ControllerAI)
                EditorTestKeys.OnAIAttack += OnTestAIAttack;
#endif
        }

        public void DetachController()
        {
            if(Controller == null) return;
#if UNITY_EDITOR
            EditorTestKeys.OnAIAttack -= OnTestAIAttack;
#endif
            Controller.Dispose();
            Destroy(Controller);
            Controller = null;
        }
        #endregion

        #region Editor
#if UNITY_EDITOR
        /// <summary>
        /// AI 캐릭터 공격 테스트용. 인스펙터에서 체크한 AI만 대상 쪽으로 돌아서서 공격을 누르고,
        /// 공격 중에 다시 누르면 다음 단계로 이어진다.
        /// AI가 직접 누르는 것과 같은 경로라 피격 중처럼 공격이 막히는 조건도 그대로 따른다
        /// </summary>
        private void OnTestAIAttack()
        {
            if (!_aiAttackTestTarget) return;
            if (Controller is not ControllerAI controllerAI) return;
            controllerAI.FaceTarget();
            controllerAI.RequestAttack();
        }
#endif
        #endregion
    }
}
