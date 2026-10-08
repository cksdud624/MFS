using Cysharp.Threading.Tasks;
using Generated.Table;
using InGame.Context;
using UniRx;
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

        public virtual async UniTask Init(InGameContext inGameContext, ObjectData objectData, bool isPlayer = false)
        {
            InGameContext = inGameContext;
            IsPlayer = isPlayer;
            InputContext = new ();
            //진영은 지금 플레이어와 나머지 둘뿐이다. 소환수처럼 플레이어 편이 생기면 스포너가 정해서 넘긴다
            ObjectContext = new(objectData, isPlayer ? Team.Player : Team.Enemy);
            AnimationPlayer = gameObject.AddComponent<AnimationPlayer>();
            await AnimationPlayer.Init(ObjectContext, objectData);
            //이펙트 프리팹은 여기서 미리 로드해두고 재생 요청 때 바로 쓴다
            EffectPlayer = gameObject.AddComponent<EffectPlayer>();
            await EffectPlayer.Init(InGameContext, ObjectContext, objectData);
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

            //맞는 판정은 캐릭터가 아닌 오브젝트도 가진다. 본체와 레이어가 달라야 하므로 자식으로 둔다.
            //Rigidbody는 PhysicsController가 붙인 본체 것을 쓰므로 그 뒤에 만든다
            var hurtBoxObject = new GameObject("HurtBox");
            hurtBoxObject.transform.SetParent(transform, false);
            HurtBox = hurtBoxObject.AddComponent<HurtBox>();
            HurtBox.Init(InGameContext, ObjectContext);
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

        protected virtual void OnDestroy()
        {
#if UNITY_EDITOR
            EditorTestKeys.OnAIAttack -= OnTestAIAttack;
#endif
            State.Value = ObjectState.Destroyed;
        }

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
    }
}
