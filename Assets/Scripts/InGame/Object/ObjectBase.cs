using System.Threading;
using Cysharp.Threading.Tasks;
using Generated.Table;
using InGame.Context;
using R3;
using UnityEngine;
using ObjectType = Common.GameDefine.ObjectType;
using ObjectState  = Common.GameDefine.ObjectState;
using Team = Common.GameDefine.Team;
using Direction = Common.GameDefine.Direction;
using EffectType = Common.GameDefine.EffectType;
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
        //Object 테이블 번호. 시나리오가 게임에 있는 대상을 넘겨받을 때 이 번호로 찾는다
        public long ObjectId => ObjectContext.ObjectData.Id;
        //진영. 생성할 때 스포너가 정한다
        public Team Team => ObjectContext.Team;
        //몸통 중심(콜라이더 중점). 원점은 발밑이라 카메라가 바라볼 곳처럼 몸을 가리킬 때는 이쪽을 쓴다
        public Vector2 Center => (Vector2)transform.position + ObjectContext.ColliderOffset;
        public ObjectType ObjectType => ObjectContext?.ObjectType ?? ObjectType.Object;
        #endregion

        #region Init
        public virtual async UniTask Init(InGameContext inGameContext, ObjectData objectData, Team team, bool isPlayer = false)
        {
            InGameContext = inGameContext;
            IsPlayer = isPlayer;
            InputContext = new ();
            ObjectContext = new(objectData, team);
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

        /// <summary>대화 같은 연출을 시작하거나 끝낸다. 연출 중에는 입력과 피격을 받지 않는다</summary>
        public void SetInEvent(bool inEvent) => ObjectContext.SetInEvent(inEvent);

        /// <summary>대상이 있는 쪽으로 돌아선다. 공격이나 피격으로 방향이 잠겨 있으면 돌지 않는다</summary>
        public void Face(Vector3 position)
        {
            float diffX = position.x - transform.position.x;
            //거의 겹쳐 있으면 좌우를 가릴 수 없으므로 보던 방향을 그대로 둔다
            if (Mathf.Abs(diffX) < Common.GameDefine.TargetDirectionDeadZone) return;
            ObjectContext.SetDirection(diffX > 0f ? Direction.Right : Direction.Left);
        }

        /// <summary>
        /// targetX까지 강제로 걸어가게 한다. 플레이어와 AI가 쓰는 입력 경로(InputContext)를 그대로 타므로 이동, 방향, 애니메이션이 같이 따라온다.
        /// seconds가 0보다 크면 그 시간에 도착하도록 속도를 맞추고, 0이면 자기 걸음 속도(MoveSpeed)로 간다.
        /// 컨트롤러(플레이어 입력, AI)가 붙어 있으면 도착할 때까지 멈춰둔다. 같은 InputContext에 쓰면 서로 덮어써서 도착하지 못한다
        /// </summary>
        public async UniTask MoveTo(float targetX, float seconds, CancellationToken cancellationToken)
        {
            float startDiffX = targetX - transform.position.x;
            if (Mathf.Abs(startDiffX) <= Common.GameDefine.ScenarioArriveDistance) return;

            SuspendControl();
            //속도는 이동 입력이 들어올 때 정해지므로 첫 입력을 내기 전에 바꿔둔다
            if (seconds > 0f)
                ObjectContext.SetMoveSpeedOverride(Mathf.Abs(startDiffX) / seconds);
            try
            {
                while (true)
                {
                    float diffX = targetX - transform.position.x;
                    //빠르게 걸으면 한 물리 스텝에 목표를 지나칠 수 있다. 지나친 것도 도착으로 봐야 앞뒤로 흔들리지 않는다
                    bool passed = Mathf.Sign(diffX) != Mathf.Sign(startDiffX);
                    if (passed || Mathf.Abs(diffX) <= Common.GameDefine.ScenarioArriveDistance) return;

                    //방향이 바뀔 때만 알린다. 매 스텝 알리면 같은 입력이 반복돼서 이동 애니메이션이 처음부터 다시 돈다
                    float directionX = Mathf.Sign(diffX);
                    if (!Mathf.Approximately(InputContext.MoveDirection.x, directionX))
                    {
                        InputContext.NotifyMove(new Vector2(directionX, 0f));
                        ObjectContext.SetDirection(directionX > 0f ? Direction.Right : Direction.Left);
                    }

                    await UniTask.WaitForFixedUpdate(cancellationToken);
                }
            }
            finally
            {
                //도착하거나 취소되면 멈추고 조작을 돌려준다. 씬이 내려가며 취소된 경우에는 이미 파괴되어 있을 수 있다.
                //시나리오가 따로 멈춰둔 것은 멈춘 횟수가 따로 세어지므로 그대로 남는다
                if (this != null)
                {
                    InputContext.NotifyMove(Vector2.zero);
                    ObjectContext.SetMoveSpeedOverride(0f);
                    ResumeControl();
                }
            }
        }

        /// <summary>
        /// 조작(플레이어 입력)이나 AI를 멈춘다. 시나리오가 강제로 제어할 때 쓴다. 컨트롤러가 없으면(NPC) 할 일이 없다.
        /// 멈춘 만큼 ResumeControl을 불러야 다시 켜진다
        /// </summary>
        public void SuspendControl()
        {
            if (Controller != null)
                Controller.Suspend();
        }

        public void ResumeControl()
        {
            if (Controller != null)
                Controller.Resume();
        }

        /// <summary>
        /// 이 오브젝트가 들고 있는 이펙트를 띄운다. 판정 없이 연출로만 나간다.
        /// offset은 오른쪽을 보는 기준이고 보는 방향에 맞춰 뒤집는다. duration이 0이면 프리팹 수명대로 재생한다
        /// </summary>
        public void PlayEffect(EffectType effectType, int variant, Vector2 offset, float duration)
            => ObjectContext.PlayEffect(effectType, variant, ObjectContext.GetDirectionalOffset(offset), duration: duration);

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
