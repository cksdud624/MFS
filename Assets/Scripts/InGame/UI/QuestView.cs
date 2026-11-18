using TMPro;
using UnityEngine;

namespace InGame.UI
{
    /// <summary>
    /// 화면 왼쪽 위의 퀘스트 안내. 지금 퀘스트의 제목과 지금 해야 할 일을 글로 띄운다.
    /// 무엇을 언제 띄울지는 StoryHudPresenter가 매 프레임 정하고 여기서는 보여주기만 한다.
    /// 프리팹은 에디터 메뉴 UI > Build Quest View가 코드로 조립해서 만든다. 폭은 고정이고 높이는 글의 줄 수에 맞춰 늘어난다
    /// </summary>
    public class QuestView : MonoBehaviour
    {
        #region Fields
        //안내 전체. 띄울 것이 없을 때는 꺼둔다
        [SerializeField] private GameObject _panel;
        //퀘스트의 제목 (QuestInfo 표의 Title)
        [SerializeField] private TMP_Text _titleText;
        //지금 해야 할 일 (기다리는 목표 줄의 Text)
        [SerializeField] private TMP_Text _goalText;
        #endregion

        #region Init
        public void Init() => Hide();
        #endregion

        #region Public
        /// <summary>
        /// 안내를 띄운다. 매 프레임 같은 내용으로 불리므로 내용이 바뀌었을 때만 글자를 다시 넣는다.
        /// 넣을 때마다 글자 메시와 레이아웃을 다시 만들기 때문이다
        /// </summary>
        public void Show(string title, string goal)
        {
            if (!_panel.activeSelf)
                _panel.SetActive(true);
            if (_titleText.text != title)
                _titleText.text = title;
            if (_goalText.text != goal)
                _goalText.text = goal;
        }

        public void Hide()
        {
            if (_panel.activeSelf)
                _panel.SetActive(false);
        }
        #endregion
    }
}
