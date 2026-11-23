using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR
using Common.Test;
using InGame.Story.Quest.Steps;
using static Common.GameDefine;
#endif

namespace InGame.Story
{
    /// <summary>
    /// 이야기를 처음부터 진행한다. 상주 대상을 세운 뒤 퀘스트를 번호 순으로 하나씩 실행한다.
    /// 퀘스트의 줄은 한 줄이 끝나야 다음 줄로 넘어가므로, 목표 줄은 그 조건을 채울 때까지 뒤의 시나리오가 나오지 않는다.
    /// 무엇을 하는지는 명령 클래스가 정하고, 여기서는 검사하고 순서대로 돌리기만 한다
    /// </summary>
    public class StoryPlayer
    {
        #region Fields
        private readonly StoryContext _context;
        #endregion

        #region Init
        public StoryPlayer(StoryContext context)
        {
            _context = context;
        }
        #endregion

        #region Public
        public async UniTask Run(CancellationToken cancellationToken)
        {
            if (!TryBuild(out var book)) return;
            _context.SetBook(book);

            foreach (var entry in book.Actors)
            {
                if (entry.Resident)
                    await _context.Spawn(entry.Actor, _context.ToWorld(entry.Zone, entry.Position));
            }

            //이야기는 첫 퀘스트부터 시작한다. 에디터의 테스트 설정으로만 중간부터 시작할 수 있다
            int startIndex = 0;
#if UNITY_EDITOR
            startIndex = await FindTestStart(book, cancellationToken);
#endif

            var quests = book.Quests;
            for (int questIndex = startIndex; questIndex < quests.Count; questIndex++)
            {
                var quest = quests[questIndex];
                _context.SetProgress(quest);
                Debug.Log($"[퀘스트] {quest.Number} 시작");
                var steps = quest.Steps;
                for (int i = 0; i < steps.Count; i++)
                {
                    var step = steps[i];
                    //이 줄이 목표라면, 채웠을 때 바로 연출이 이어지는지를 목표 아이콘이 알 수 있게 미리 남긴다
                    _context.SetScenarioNext(i + 1 < steps.Count && steps[i + 1].PlaysScenario);
                    Debug.Log($"[퀘스트] {step} {step.Actor}");
                    await step.Run(_context, cancellationToken);
                }
                Debug.Log($"[퀘스트] {quest.Number} 완료");
            }

            Debug.Log("[퀘스트] 모든 퀘스트 완료");
        }
        #endregion

        #region Private
        /// <summary>
        /// 시작하기 전에 에디터 메뉴(Table > Validate Story)와 같은 정합성 검사를 한다. 틀린 곳이 있으면 시작하지 않는다.
        /// 건너뛰고 진행하면 뒤의 줄이 없는 대상을 찾거나 조건을 못 채워서 멈춘 곳이 원인과 멀어진다.
        /// 이펙트 프리팹은 런타임에서 경로로 바로 확인할 수 없어서 에디터 메뉴에서만 본다
        /// </summary>
        private bool TryBuild(out StoryBook book)
        {
            var tableManager = _context.TableManager;
            var validator = new StoryValidator(new TableStoryReferences(tableManager, _context.ZoneLayout));
            if (validator.Validate(new StoryTables(tableManager), out book)) return true;

            Debug.LogError("Story does not start. Fix the errors above (Table > Validate Story shows them without Play).");
            return false;
        }
        #endregion

        #region Editor
#if UNITY_EDITOR
        /// <summary>
        /// 테스트 설정(메뉴 Test > Start At Boss Battle)이 켜져 있으면 보스전이 있는 첫 퀘스트부터 시작한다. 시작할 퀘스트의 순번을 돌려준다.
        /// 앞의 퀘스트를 건너뛰므로 거기서 세웠을 플레이어를 보스가 설 자리 근처에 직접 세운다.
        /// 보스가 서는 Zone 안쪽에 세워서, 보스전 줄에 닿자마자 Zone이 잠기고 전투가 시작되게 한다.
        /// 앞 퀘스트가 세웠을 다른 대상은 월드에 없다. 보스전 퀘스트가 그런 대상을 쓰면 그 줄에서 에러가 난다
        /// </summary>
        private async UniTask<int> FindTestStart(StoryBook book, CancellationToken cancellationToken)
        {
            if (!EditorTestSettings.StartAtBossBattle) return 0;

            for (int questIndex = 0; questIndex < book.Quests.Count; questIndex++)
            {
                foreach (var step in book.Quests[questIndex].Steps)
                {
                    if (step is not BossStep) continue;

                    var boss = book.GetActor(step.Actor);
                    Debug.Log($"[테스트 설정] 보스전부터 시작 : 퀘스트 {book.Quests[questIndex].Number} ({step.Actor})");
                    await _context.SpawnPlayerForTest(boss.Zone, GetStartPosition(boss), cancellationToken);
                    return questIndex;
                }
            }

            Debug.LogWarning("[테스트 설정] 보스전(Boss 명령)이 있는 퀘스트가 없어서 첫 퀘스트부터 시작한다");
            return 0;
        }

        //보스가 서는 자리에서 왼쪽으로 조금 떨어진 곳 (Zone 로컬 좌표). 보스전이 시작되는 범위(경계에서 BossArenaEnterMargin 안쪽)를 벗어나지 않게 한다
        private Vector2 GetStartPosition(StoryBook.ActorEntry boss)
        {
            float x = boss.Position.x - EditorTestSettings.BossBattleStartDistance;
            if (_context.ZoneLayout.TryGetZone(boss.Zone, out var zone))
            {
                float margin = BossArenaEnterMargin + 1f;
                x = Mathf.Clamp(x, margin, zone.Rect.width - margin);
            }
            return new Vector2(x, boss.Position.y);
        }
#endif
        #endregion
    }
}
