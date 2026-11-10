using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Generated.Table;
using InGame.Context;
using InGame.Object;
using InGame.UI;

namespace InGame.Story
{
    /// <summary>
    /// 대사 목록을 대화창에 차례로 띄운다. 시나리오의 Dialogue와 일반 대화가 같이 쓴다.
    /// Interact를 누르면 한 글자씩 나오던 대사는 다 보여주고, 다 나온 대사면 다음 대사로 넘긴다
    /// </summary>
    public class DialoguePlayer
    {
        #region Fields
        private readonly InGameContext _inGameContext;
        private readonly DialogueView _view;
        #endregion

        #region Init
        public DialoguePlayer(InGameContext inGameContext, DialogueView view)
        {
            _inGameContext = inGameContext;
            _view = view;
        }
        #endregion

        #region Public
        /// <summary>
        /// 플레이어와 대화 상대(없으면 null)가 마주 보고 대화한다. 대화 중에는 플레이어를 Event 상태로 묶어서 움직이거나 맞지 않게 한다.
        /// 마지막 대사를 넘길 때까지 기다린다
        /// </summary>
        public async UniTask PlayConversation(ObjectBase partner, IReadOnlyList<DialogueData> lines, CancellationToken cancellationToken)
        {
            var player = _inGameContext.Player;
            if (player == null) return;

            //공격 중이면 방향이 잠겨 있으므로 Event로 공격을 끊은 뒤에 돌린다
            player.SetInEvent(true);
            try
            {
                if (partner != null)
                {
                    player.Face(partner.transform.position);
                    partner.Face(player.transform.position);
                }
                await Play(lines, cancellationToken);
            }
            finally
            {
                //대화가 취소되어도 Event를 풀어둔다. 남으면 플레이어가 입력과 피격을 받지 않는 채로 굳는다.
                //씬이 내려가며 취소된 경우에는 이미 파괴되어 있을 수 있다
                if (player != null)
                    player.SetInEvent(false);
            }
        }
        #endregion

        #region Private
        //마지막 대사를 넘길 때까지 기다린다. 끝나거나 취소되면 대화창을 닫는다
        private async UniTask Play(IReadOnlyList<DialogueData> lines, CancellationToken cancellationToken)
        {
            try
            {
                foreach (var line in lines)
                {
                    _view.ShowLine(line.Speaker, line.Text);
                    await _inGameContext.WaitInteract(cancellationToken);

                    //나오던 중이면 이번 입력으로는 다 보여주기만 한다. 바로 넘기면 끝까지 읽기 전에 사라진다
                    if (!_view.IsTyping) continue;
                    _view.CompleteLine();
                    await _inGameContext.WaitInteract(cancellationToken);
                }
            }
            finally
            {
                //씬이 내려가며 취소된 경우에는 대화창이 먼저 파괴되어 있을 수 있다
                if (_view != null)
                    _view.Hide();
            }
        }
        #endregion
    }
}
