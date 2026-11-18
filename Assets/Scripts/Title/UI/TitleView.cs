using System;
using UnityEngine;
using UnityEngine.UI;

namespace Title.UI
{
    /// <summary>
    /// 타이틀 화면. 게임 이름과 메뉴(시작, 이어하기, 종료)를 띄우고, 고른 메뉴를 이벤트로 알린다.
    /// 메뉴는 키보드(위아래로 고르고 Enter)와 마우스로 고른다. 골랐을 때 무엇을 할지는 TitleController가 정한다.
    /// 이어하기는 저장 기능이 생길 때까지 고를 수 없게 프리팹에서 꺼둔다. 그래서 여기서는 다루지 않는다.
    /// 프리팹은 에디터 메뉴 UI > Build Title View가 코드로 조립해서 만든다
    /// </summary>
    public class TitleView : MonoBehaviour
    {
        #region Fields
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _quitButton;

        public event Action OnStartRequested;
        public event Action OnQuitRequested;
        #endregion

        #region Init
        public void Init()
        {
            _startButton.onClick.AddListener(HandleStartClicked);
            _quitButton.onClick.AddListener(HandleQuitClicked);
            //키보드는 선택된 버튼에서부터 움직인다. 아무것도 선택되어 있지 않으면 방향키와 Enter가 듣지 않으므로 첫 메뉴를 골라 둔다
            _startButton.Select();
        }
        #endregion

        #region LifeCycle
        private void OnDestroy()
        {
            _startButton.onClick.RemoveListener(HandleStartClicked);
            _quitButton.onClick.RemoveListener(HandleQuitClicked);
        }
        #endregion

        #region Event Handlers
        private void HandleStartClicked() => OnStartRequested?.Invoke();
        private void HandleQuitClicked() => OnQuitRequested?.Invoke();
        #endregion
    }
}
