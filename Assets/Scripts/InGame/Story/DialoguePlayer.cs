using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Generated.Table;
using InGame.Context;
using InGame.Object;
using InGame.UI;
using UnityEngine;

namespace InGame.Story
{
    /// <summary>
    /// 대사를 대화창에 띄운다. 일반 대화, 퀘스트, 시나리오가 같이 쓴다.
    /// 대화를 통째로 띄울 때(PlayConversation)는 끝나면 창을 닫고, 한 줄씩 띄울 때(PlayLine)는 창을 열어둔 채 둬서 대사 사이에 연출을 끼울 수 있게 한다.
    /// Interact를 누르면 한 글자씩 나오던 대사는 다 보여주고, 다 나온 대사면 다음 대사로 넘긴다
    /// </summary>
    public class DialoguePlayer
    {
        #region Fields
        private readonly InGameContext _inGameContext;
        private readonly DialogueView _view;

        //대화창이 열려 있는지. 열려 있는 동안 누른 Interact는 대사를 넘기는 입력이라 일반 대화나 말 걸기 목표로 쓰지 않는다.
        //닫힌 프레임까지는 열린 것으로 본다. 마지막 대사를 넘겨 창을 닫은 입력을 같은 프레임에 받는 쪽이 말 걸기로 다시 쓰지 않게 한다
        public bool IsOpen => _isOpen || _closedFrame == Time.frameCount;

        //창이 지금 열려 있는지
        private bool _isOpen;
        //창을 닫은 프레임
        private int _closedFrame = -1;
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
        /// 플레이어와 대화 상대(없으면 null)가 마주 보고 대화 하나를 끝까지 한다. 마지막 대사를 넘기면 창을 닫는다.
        /// 대화 내내 플레이어를 Event 상태로 묶어서 움직이거나 맞지 않게 한다
        /// </summary>
        public async UniTask PlayConversation(ObjectBase partner, IReadOnlyList<DialogueData> lines, CancellationToken cancellationToken)
        {
            var player = _inGameContext.Player;
            if (player == null) return;

            //호출한 순간 열린 것으로 둔다. 같은 입력을 받는 다른 쪽(말 걸기 목표)이 이 입력을 대사 넘기기로 보게 하려는 것이다
            _isOpen = true;
            player.SetInEvent(true);
            try
            {
                Face(player, partner);
                foreach (var line in lines)
                    await ShowAndWait(line, cancellationToken);
            }
            finally
            {
                ReleasePlayer(player);
                Close();
            }
        }

        /// <summary>
        /// 대사 한 줄을 띄우고 넘길 때까지 기다린다. 창은 닫지 않아서 다음 연출 동안 마지막 대사가 남는다.
        /// 플레이어는 읽는 동안만 Event로 묶는다. 묶은 채로 두면 대사 사이의 Move로 플레이어를 움직일 수 없다
        /// </summary>
        public async UniTask PlayLine(ObjectBase partner, DialogueData line, CancellationToken cancellationToken)
        {
            var player = _inGameContext.Player;
            if (player == null) return;

            _isOpen = true;
            player.SetInEvent(true);
            try
            {
                Face(player, partner);
                await ShowAndWait(line, cancellationToken);
            }
            finally
            {
                ReleasePlayer(player);
            }
        }

        /// <summary>대화창을 닫는다. 한 줄씩 띄운 대화를 끝낼 때(CloseDialogue, 시나리오 끝) 부른다</summary>
        public void Close()
        {
            if (_isOpen)
                _closedFrame = Time.frameCount;
            _isOpen = false;
            //씬이 내려가며 취소된 경우에는 대화창이 먼저 파괴되어 있을 수 있다
            if (_view != null)
                _view.Hide();
        }
        #endregion

        #region Private
        private async UniTask ShowAndWait(DialogueData line, CancellationToken cancellationToken)
        {
            _view.ShowLine(line.Speaker, line.Text);
            await _inGameContext.WaitInteract(cancellationToken);

            //나오던 중이면 이번 입력으로는 다 보여주기만 한다. 바로 넘기면 끝까지 읽기 전에 사라진다
            if (!_view.IsTyping) return;
            _view.CompleteLine();
            await _inGameContext.WaitInteract(cancellationToken);
        }

        //공격 중이면 방향이 잠겨 있으므로 Event로 공격을 끊은 뒤에 불러야 돌아선다
        private static void Face(ObjectBase player, ObjectBase partner)
        {
            if (partner == null) return;
            player.Face(partner.transform.position);
            partner.Face(player.transform.position);
        }

        //대화가 취소되어도 Event를 풀어둔다. 남으면 플레이어가 입력과 피격을 받지 않는 채로 굳는다.
        //씬이 내려가며 취소된 경우에는 이미 파괴되어 있을 수 있다
        private static void ReleasePlayer(ObjectBase player)
        {
            if (player != null)
                player.SetInEvent(false);
        }
        #endregion
    }
}
