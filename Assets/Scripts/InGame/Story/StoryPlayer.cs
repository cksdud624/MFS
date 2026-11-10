using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

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

            foreach (var actor in book.Actors)
            {
                if (actor.Resident)
                    await _context.Spawn(StoryActor.FromNumber(actor.Number), actor.Position);
            }

            foreach (var quest in book.Quests)
            {
                _context.SetProgress(quest.Number);
                Debug.Log($"[퀘스트] {quest.Number} 시작");
                foreach (var step in quest.Steps)
                {
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
            var validator = new StoryValidator(new TableStoryReferences(tableManager));
            if (validator.Validate(new StoryTables(tableManager), out book)) return true;

            Debug.LogError("Story does not start. Fix the errors above (Table > Validate Story shows them without Play).");
            return false;
        }
        #endregion
    }
}
