using System;
using System.Collections.Generic;
using Common;
using Common.Template.Interface;
using InGame.Object;
using InGame.UI;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Story
{
    /// <summary>
    /// 이야기의 진행을 화면에 알려주는 UI를 정한다. 이야기의 상태(StoryContext)를 매 프레임 읽어서 뷰에 넘긴다.
    /// - 퀘스트 안내 (QuestView) : 지금 퀘스트의 제목과, 기다리는 목표가 시키는 일. 기다리는 목표가 있을 때만 띄운다.
    /// - 퀘스트 / 시나리오 아이콘 (MarkerView) : 그 목표가 가리키는 곳 (말을 걸 Npc, 가야 할 곳, 쓰러뜨릴 적). 거리와 상관없이 띄운다.
    ///   목표를 채우면 바로 시나리오가 이어지면 시나리오 아이콘, 아니면 퀘스트 아이콘이다.
    /// - 대화 아이콘 (MarkerView) : 지금 할 일반 대화가 있는 Npc. 플레이어가 가까이 왔을 때만 띄운다.
    /// - 보스 체력바 (BossView) : 보스전을 벌이는 동안 보스의 이름과 남은 Hp, 페이즈.
    /// 시나리오 재생 중과 대화 중에는 모두 숨긴다. 무엇을 띄울지는 Quest, QuestInfo, Talk, Actor 표에서 정해지는 것을 그대로 쓴다
    /// </summary>
    public class StoryHudPresenter : ILateUpdateable, IDisposable
    {
        #region Fields
        private readonly StoryContext _context;
        //프리팹이 없으면 null. 있는 것만 띄운다
        private readonly MarkerView _markerView;
        private readonly QuestView _questView;
        private readonly BossView _bossView;

        //이번 프레임에 띄울 아이콘. 매 프레임 비우고 다시 채운다
        private readonly List<MarkerView.Marker> _markers = new();
        //월드의 대상을 훑을 때 쓰는 임시 목록. 매번 할당하지 않게 재사용한다
        private readonly List<KeyValuePair<StoryActor, ObjectBase>> _actors = new();
        #endregion

        #region Init
        public StoryHudPresenter(StoryContext context, MarkerView markerView, QuestView questView, BossView bossView)
        {
            _context = context;
            _markerView = markerView;
            _questView = questView;
            _bossView = bossView;

            Global.Instance.BindLateUpdate(this);
        }
        #endregion

        #region LifeCycle
        //대상이 이번 프레임에 움직인 뒤에 그 자리를 읽는다
        public void OnLateUpdate()
        {
            bool canShow = CanShow();
            UpdateQuestGuide(canShow);
            UpdateMarkers(canShow);
            UpdateBoss(canShow);
        }
        #endregion

        #region Public
        public void Dispose() => Global.Instance?.UnBindLateUpdate(this);
        #endregion

        #region Private
        //이야기가 시작되기 전(검사 실패 포함)에는 띄울 것이 없고, 연출과 대화 중에는 화면을 가리지 않게 숨긴다
        private bool CanShow()
            => _context.Book != null && !_context.IsInScenario && !_context.IsInDialogue && _context.InGameContext.Player != null;

        /// <summary>
        /// 퀘스트 안내는 기다리는 목표가 있을 때만 띄운다. 목표 사이(대화, 연출로 넘어가는 중)에는 플레이어가 할 일이 없다
        /// </summary>
        private void UpdateQuestGuide(bool canShow)
        {
            //씬이 내려가며 뷰가 먼저 파괴되었을 수 있다
            if (_questView == null) return;

            var quest = _context.Quest;
            if (canShow && quest != null && _context.Goal.Target != StoryGoal.TargetKind.None)
                _questView.Show(quest.Title, _context.GoalText);
            else
                _questView.Hide();
        }

        private void UpdateMarkers(bool canShow)
        {
            if (_markerView == null) return;

            _markers.Clear();
            if (canShow)
            {
                AddGoalMarkers();
                AddTalkMarkers();
            }
            _markerView.Show(_markers);
        }

        /// <summary>
        /// 보스 체력바는 보스전을 벌이는 동안만 띄운다. 보스가 쓰러지는 모션을 하는 동안에는 빈 체력바가 남아 있다가,
        /// 보스가 사라져서 전투가 끝나면 같이 사라진다
        /// </summary>
        private void UpdateBoss(bool canShow)
        {
            if (_bossView == null) return;

            var actor = _context.BattleBoss;
            if (canShow && !actor.IsNone && _context.TryFindActor(actor, out var boss))
                _bossView.Show(_context.Book.GetActorName(actor), boss.Hp, boss.MaxHp, boss.Phase, boss.PhaseCount, boss.PatternName);
            else
                _bossView.Hide();
        }

        private void AddGoalMarkers()
        {
            var goal = _context.Goal;
            var kind = _context.IsScenarioNext ? StoryMarkerKind.Scenario : StoryMarkerKind.Quest;
            switch (goal.Target)
            {
                case StoryGoal.TargetKind.Actor:
                    if (_context.TryFindActor(goal.Actor, out var target))
                        _markers.Add(new MarkerView.Marker(kind, target.Top));
                    break;
                case StoryGoal.TargetKind.Enemies:
                    _context.CollectActors(StoryActorType.Enemy, _actors);
                    foreach (var pair in _actors)
                        _markers.Add(new MarkerView.Marker(kind, pair.Value.Top));
                    break;
                case StoryGoal.TargetKind.Point:
                    _markers.Add(new MarkerView.Marker(kind, goal.Point + new Vector2(0f, GoalPointMarkerHeight)));
                    break;
            }
        }

        private void AddTalkMarkers()
        {
            float playerX = _context.InGameContext.Player.transform.position.x;
            var goal = _context.Goal;

            _context.CollectActors(StoryActorType.Npc, _actors);
            foreach (var pair in _actors)
            {
                //Zone이 내려가 꺼진 Npc는 화면에 없어서 말을 걸 수 없다
                if (!pair.Value.gameObject.activeInHierarchy) continue;
                //목표가 가리키는 Npc는 목표 아이콘이 뜬다. 이 Npc에게 건 말도 일반 대화가 아니라 목표로 간다
                if (goal.Target == StoryGoal.TargetKind.Actor && goal.Actor.Equals(pair.Key)) continue;
                if (_context.Book.FindTalk(pair.Key, _context.Progress) == 0) continue;
                //말 걸기와 같이 좌우 거리로 본다
                if (Mathf.Abs(pair.Value.transform.position.x - playerX) > TalkMarkerDistance) continue;

                _markers.Add(new MarkerView.Marker(StoryMarkerKind.Talk, pair.Value.Top));
            }
        }
        #endregion
    }
}
