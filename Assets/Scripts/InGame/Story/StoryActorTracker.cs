using System.Collections.Generic;
using static Common.GameDefine;

namespace InGame.Story
{
    /// <summary>
    /// 검사기가 월드에 어떤 대상이 있는지 따라가는 장부. 대상이 들어오고(Spawn, 상주) 나가는 것(Despawn, Clear)만 기록하고 물어본다.
    /// 대상은 시나리오나 퀘스트가 아니라 월드가 가지므로, 장부는 퀘스트와 시나리오를 넘어 이야기 끝까지 이어진다.
    /// 무엇이 틀렸는지 판단하고 에러를 남기는 것은 StoryValidator가 한다. 상태와 규칙을 나눠서 규칙 쪽이 짧게 읽히게 한다
    /// </summary>
    public class StoryActorTracker
    {
        #region Fields
        //월드에 있는 대상과 그 종류
        private readonly Dictionary<StoryActor, StoryActorType> _alive = new();
        //월드에서 나간 대상. "아직 안 들어옴"과 "이미 나감"을 구분해서 알려주려고 둔다
        private readonly HashSet<StoryActor> _ended = new();
        //목표를 지나는 동안 월드에 있던 적. 플레이어와 싸워서 이미 쓰러졌을 수 있다
        private readonly HashSet<StoryActor> _atRisk = new();
        //EndAll이 지울 대상을 모아두는 곳. 순회 중인 사전은 고칠 수 없어서 따로 모은 뒤 지운다
        private readonly List<StoryActor> _toEnd = new();

        //플레이어를 한 번이라도 세웠는지. 플레이어는 하나뿐이고 없앨 수 없어서 두 번 세울 수 없다
        public bool PlayerSpawned { get; private set; }
        #endregion

        #region Public
        public void Clear()
        {
            _alive.Clear();
            _ended.Clear();
            _atRisk.Clear();
            PlayerSpawned = false;
        }

        public void Start(StoryActor actor, StoryActorType type)
        {
            _alive[actor] = type;
            _ended.Remove(actor);
            _atRisk.Remove(actor);
            if (actor.IsPlayer)
                PlayerSpawned = true;
        }

        public void End(StoryActor actor)
        {
            _alive.Remove(actor);
            _atRisk.Remove(actor);
            _ended.Add(actor);
        }

        /// <summary>그 종류의 대상을 모두 내보낸다 (Clear가 적을 모두 기다렸을 때)</summary>
        public void EndAll(StoryActorType type)
        {
            _toEnd.Clear();
            foreach (var pair in _alive)
            {
                if (pair.Value == type)
                    _toEnd.Add(pair.Key);
            }
            foreach (var actor in _toEnd)
                End(actor);
        }

        /// <summary>지금 월드에 있는 그 종류의 대상을 모두 "쓰러졌을 수 있음"으로 표시한다. 목표를 지날 때 부른다</summary>
        public void MarkAtRisk(StoryActorType type)
        {
            foreach (var pair in _alive)
            {
                if (pair.Value == type)
                    _atRisk.Add(pair.Key);
            }
        }

        public bool IsAlive(StoryActor actor) => _alive.ContainsKey(actor);
        public bool IsEnded(StoryActor actor) => _ended.Contains(actor);
        public bool IsAtRisk(StoryActor actor) => _atRisk.Contains(actor);

        /// <summary>그 종류의 대상이 하나라도 월드에 있는지</summary>
        public bool HasAlive(StoryActorType type)
        {
            foreach (var pair in _alive)
            {
                if (pair.Value == type)
                    return true;
            }
            return false;
        }
        #endregion
    }
}
