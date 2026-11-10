using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Generated.Table;
using InGame.Context;
using InGame.Object;
using InGame.UI;
using InGame.World;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Story
{
    /// <summary>
    /// 시나리오, 퀘스트, 일반 대화가 실행 중에 같이 쓰는 것. 생성과 대화 같은 기능, 그리고 월드에 세운 대상(Actor)을 들고 있다.
    /// 대상은 시나리오나 퀘스트가 아니라 월드가 가지므로, 앞 퀘스트에서 세운 대상을 뒤의 줄이 같은 번호로 찾는다.
    /// 명령은 서로를 모르고 이 컨텍스트만 거친다
    /// </summary>
    public class StoryContext
    {
        #region Fields
        public InGameContext InGameContext { get; }
        public ObjectSpawner ObjectSpawner { get; }
        //명령이 읽는 표. 명령이 Global을 직접 찾지 않고 이 컨텍스트만 거치게 하려고 받아둔다
        public TableManager TableManager { get; }
        //UI 프리팹이나 카메라가 없으면 null. 이때 그것을 쓰는 명령은 에러를 남기고 건너뛴다.
        //배치와 전투는 이것들 없이도 확인할 수 있어야 하므로 이야기 자체는 막지 않는다
        public DialoguePlayer DialoguePlayer { get; }
        public ScreenFadeView ScreenFade { get; }
        public CameraDirector CameraDirector { get; }
        //표의 위치(Zone 로컬 좌표)를 월드 좌표로 바꿀 때 쓰는 Zone 배치
        public ZoneLayout ZoneLayout { get; }
        //Zone을 불러오고 내리는 쪽. 세운 대상을 맡겨서, 대상이 있는 Zone이 내려가면 꺼두게 한다
        private readonly ZoneStreamer _zoneStreamer;

        //검사를 통과한 이야기. 이야기를 시작할 때 정해진다
        public StoryBook Book { get; private set; }
        //지금 진행 중인 퀘스트. 일반 대화가 진행에 맞는 대사를 고른다
        public long Progress { get; private set; }

        //월드에 세운 대상. 쓰러진 적은 파괴되어 null로 남다가 Clear가 정리한다
        private readonly Dictionary<StoryActor, ObjectBase> _actors = new();
        //IsEnemyCleared가 정리할 대상을 모아두는 곳. 순회 중인 사전은 고칠 수 없어서 따로 모은 뒤 지운다
        private readonly List<StoryActor> _toRemove = new();

        //지금 시나리오(연출)가 재생 중인지. 이 동안 월드의 조작과 AI가 모두 멈추고 일반 대화를 받지 않는다
        public bool IsInScenario { get; private set; }
        //시나리오가 멈춰둔 대상. 끝날 때 이것만 다시 켠다. 이동(Move)이 따로 멈춘 것은 멈춘 횟수가 따로 세어지므로 그대로 남는다
        private readonly List<ObjectBase> _scenarioHeld = new();

        //대화창이 열려 있는지 (일반 대화, 퀘스트 대화, 시나리오 대사). 이 동안 누른 Interact는 대사를 넘기는 입력이라
        //일반 대화와 말 걸기 목표는 받지 않고, 퀘스트의 대화와 시나리오는 창이 닫힐 때까지 시작을 기다린다
        public bool IsInDialogue => DialoguePlayer != null && DialoguePlayer.IsOpen;
        //말 걸기 목표(Interact)가 기다리는 대상. 이 대상에게 건 말은 일반 대화가 아니라 목표로 간다
        public StoryActor InteractTarget { get; private set; }
        #endregion

        #region Init
        public StoryContext(InGameContext inGameContext, ObjectSpawner objectSpawner, TableManager tableManager,
            DialoguePlayer dialoguePlayer, ScreenFadeView screenFade, CameraDirector cameraDirector,
            ZoneLayout zoneLayout, ZoneStreamer zoneStreamer)
        {
            InGameContext = inGameContext;
            ObjectSpawner = objectSpawner;
            TableManager = tableManager;
            DialoguePlayer = dialoguePlayer;
            ScreenFade = screenFade;
            CameraDirector = cameraDirector;
            ZoneLayout = zoneLayout;
            _zoneStreamer = zoneStreamer;
        }
        #endregion

        #region Public - World
        /// <summary>표에 적은 위치(Zone의 왼쪽 아래를 0,0으로 보는 로컬 좌표)를 월드 좌표로 바꾼다</summary>
        public Vector2 ToWorld(long zoneId, Vector2 local) => ZoneLayout.ToWorld(zoneId, local);
        #endregion

        #region Public - Progress
        public void SetBook(StoryBook book) => Book = book;

        public void SetProgress(long quest) => Progress = quest;
        #endregion

        #region Public - Talk
        public void SetInteractTarget(StoryActor actor) => InteractTarget = actor;

        /// <summary>플레이어와 좌우 거리가 distance 안인 Npc 중 가장 가까운 것을 찾는다. 말 걸기와 같은 거리 기준을 쓴다</summary>
        public bool TryFindNearestNpc(float x, float distance, out StoryActor actor, out ObjectBase target)
        {
            actor = StoryActor.None;
            target = null;
            float nearest = distance;
            foreach (var pair in _actors)
            {
                //Zone이 내려가 꺼진 Npc는 화면에 없으므로 말을 걸 수 없다
                if (pair.Value == null || !pair.Value.gameObject.activeInHierarchy
                    || Book.GetActorType(pair.Key) != StoryActorType.Npc) continue;

                float gap = Mathf.Abs(pair.Value.transform.position.x - x);
                if (gap > nearest) continue;
                nearest = gap;
                actor = pair.Key;
                target = pair.Value;
            }
            return target != null;
        }
        #endregion

        #region Public - Actor
        /// <summary>
        /// 대상을 월드에 세운다. 종류에 따라 붙는 것이 다르다: 플레이어는 조작, 적은 AI, Npc는 아무것도 붙지 않는다.
        /// 생성이 실패하면(데이터나 에셋 누락) null을 돌려준다
        /// </summary>
        public async UniTask<ObjectBase> Spawn(StoryActor actor, Vector2 position)
        {
            ObjectBase spawned = null;
            if (actor.IsPlayer)
            {
                await ObjectSpawner.SpawnPlayer(GetObjectData(PlayerObjectId), position);
                spawned = InGameContext.Player;
            }
            else
            {
                var entry = Book.GetActor(actor);
                var objectData = GetObjectData(entry.ObjectId);
                spawned = entry.Type == StoryActorType.Enemy
                    ? await ObjectSpawner.SpawnEnemy(objectData, position)
                    : await ObjectSpawner.SpawnNpc(objectData, position);
            }

            if (spawned == null) return null;
            _actors[actor] = spawned;
            //시나리오 도중에 등장한 대상이 연출 도중 움직이지 않게 같이 멈춘다
            if (IsInScenario)
                HoldForScenario(spawned);
            //아직 불러오지 않은 Zone에 세웠으면 바닥이 생길 때까지 꺼둔다
            _zoneStreamer?.Track(spawned);
            return spawned;
        }

        /// <summary>대상을 월드에서 없앤다</summary>
        public void Despawn(StoryActor actor)
        {
            if (!_actors.Remove(actor, out var target)) return;
            ObjectSpawner.Despawn(target);
        }

        /// <summary>
        /// 대상을 찾는다. 검사를 통과했으면 늘 찾아지지만, 생성이 실패했거나(에셋 누락) 다른 이유로 사라졌을 수 있어서 그 줄의 에러로 남긴다
        /// </summary>
        public bool TryGetActor(StoryStep step, out ObjectBase actor)
        {
            if (_actors.TryGetValue(step.Actor, out actor) && actor != null) return true;

            Debug.LogError($"{step} : {step.Actor} is not found.");
            return false;
        }

        /// <summary>대상이 쓰러졌는지(파괴되었는지). 쓰러졌으면 월드 목록에서도 뺀다. 세운 적이 없으면 이미 없는 것으로 본다</summary>
        public bool IsDefeated(StoryActor actor)
        {
            if (_actors.TryGetValue(actor, out var target) && target != null) return false;

            _actors.Remove(actor);
            return true;
        }

        /// <summary>월드의 적이 모두 쓰러졌는지. 파괴된 적은 Unity의 null 비교로 걸러내고 목록에서 뺀다</summary>
        public bool IsEnemyCleared()
        {
            _toRemove.Clear();
            bool cleared = true;
            foreach (var pair in _actors)
            {
                if (Book.GetActorType(pair.Key) != StoryActorType.Enemy) continue;
                if (pair.Value == null)
                    _toRemove.Add(pair.Key);
                else
                    cleared = false;
            }
            foreach (var actor in _toRemove)
                _actors.Remove(actor);
            return cleared;
        }
        #endregion

        #region Public - Scenario
        /// <summary>시나리오를 시작한다. 월드에 있는 대상의 조작과 AI를 모두 멈춘다</summary>
        public void BeginScenario()
        {
            IsInScenario = true;
            foreach (var actor in _actors.Values)
                HoldForScenario(actor);
        }

        /// <summary>시나리오를 끝낸다. 시나리오가 멈춘 대상만 다시 켠다</summary>
        public void EndScenario()
        {
            //시나리오 안에서 Despawn된 대상은 파괴되어 null로 남아 있다
            foreach (var actor in _scenarioHeld)
            {
                if (actor != null)
                    actor.ResumeControl();
            }
            _scenarioHeld.Clear();
            IsInScenario = false;
        }
        #endregion

        #region Public - Table
        public ObjectData GetObjectData(long objectId)
        {
            var objectData = TableManager.ObjectRecord.GetRecord(objectId);
            if (objectData == null)
                Debug.LogError($"Object {objectId} is not defined.");
            return objectData;
        }

        /// <summary>대화의 대사 목록. 없는 대화면 null</summary>
        public IReadOnlyList<DialogueData> GetDialogueLines(long dialogueId) => TableManager.DialogueRecord.GetLines(dialogueId);
        #endregion

        #region Private
        //컨트롤러가 없는 대상(Npc)은 SuspendControl이 아무것도 하지 않으므로 종류를 가리지 않고 부른다
        private void HoldForScenario(ObjectBase actor)
        {
            if (actor == null) return;
            actor.SuspendControl();
            _scenarioHeld.Add(actor);
        }
        #endregion
    }
}
