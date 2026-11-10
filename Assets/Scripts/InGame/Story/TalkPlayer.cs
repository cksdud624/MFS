using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Object;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Story
{
    /// <summary>
    /// 일반 대화. 퀘스트 목표가 아닐 때 플레이어가 Npc 가까이에서 Interact(F)를 누르면 Talk 표의 대화를 띄운다.
    /// 무슨 대화를 할지는 진행(지금 퀘스트)으로 정한다. 이미 시작된 퀘스트의 대화 중 가장 늦은 것을 고른다.
    /// 시나리오 재생 중이나 다른 대화 중에는 받지 않고, 말 걸기 목표가 기다리는 Npc에게 건 말은 목표로 넘긴다
    /// </summary>
    public class TalkPlayer : IDisposable
    {
        #region Fields
        private readonly StoryContext _context;
        //씬이 내려가면 대화를 끊는다
        private readonly CancellationToken _cancellationToken;
        #endregion

        #region Init
        public TalkPlayer(StoryContext context, CancellationToken cancellationToken)
        {
            _context = context;
            _cancellationToken = cancellationToken;

            //대사 넘기기나 말 걸기 목표보다 먼저 받아야 같은 입력이 두 곳에 쓰이지 않는다
            _context.InGameContext.OnInteractFirst += HandleInteract;
        }
        #endregion

        #region Public
        public void Dispose() => _context.InGameContext.OnInteractFirst -= HandleInteract;
        #endregion

        #region Event Handlers
        private void HandleInteract()
        {
            //이야기가 시작되기 전(검사 실패 포함)에는 진행을 알 수 없다
            if (_context.Book == null || _context.IsInScenario || _context.IsTalking || _context.DialoguePlayer == null) return;

            var player = _context.InGameContext.Player;
            if (player == null) return;
            if (!_context.TryFindNearestNpc(player.transform.position.x, InteractDistance, out var actor, out var partner)) return;
            if (actor.Equals(_context.InteractTarget)) return;

            long dialogueId = _context.Book.FindTalk(actor, _context.Progress);
            if (dialogueId == 0) return;

            Play(actor, partner, dialogueId).Forget();
        }
        #endregion

        #region Private
        private async UniTaskVoid Play(StoryActor actor, ObjectBase partner, long dialogueId)
        {
            var lines = _context.GetDialogueLines(dialogueId);
            //Talk 표의 대화 번호는 검사에서 확인했다. 여기서 없으면 표를 불러오는 데 실패한 것이다
            if (lines == null)
            {
                Debug.LogError($"Talk {actor} : Dialogue {dialogueId} is not found.");
                return;
            }

            Debug.Log($"[일반 대화] {actor} : Dialogue {dialogueId}");
            _context.SetTalking(true);
            try
            {
                await _context.DialoguePlayer.PlayConversation(partner, lines, _cancellationToken);
            }
            finally
            {
                _context.SetTalking(false);
            }
        }
        #endregion
    }
}
