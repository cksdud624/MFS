using Common;
using Common.Template.Interface;
using TMPro;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.UI
{
    /// <summary>
    /// 화면 아래쪽 대화창. 말하는 사람과 대사를 띄우고, 대사는 한 글자씩 드러낸다.
    /// 무엇을 언제 띄울지는 DialoguePlayer가 정하고 여기서는 보여주기만 한다.
    /// 프리팹은 에디터 메뉴 UI > Build Dialogue View가 코드로 조립해서 만든다
    /// </summary>
    public class DialogueView : MonoBehaviour, IUpdateable
    {
        #region Fields
        //대화창 전체. 대화가 없을 때는 꺼둔다
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _speakerText;
        [SerializeField] private TMP_Text _lineText;
        //다음 대사로 넘길 수 있다는 표시. 대사가 다 나온 뒤에만 켠다
        [SerializeField] private GameObject _nextHint;

        //지금까지 드러낸 글자 수. 소수점까지 세서 프레임 간격과 상관없이 같은 속도로 나오게 한다
        private float _visibleCount;
        //지금 대사의 전체 글자 수. 리치 텍스트 태그는 빼고 센다
        private int _totalCount;

        //대사가 아직 한 글자씩 나오는 중인지
        public bool IsTyping => _visibleCount < _totalCount;
        #endregion

        #region Init
        public void Init()
        {
            Hide();
            Global.Instance.BindUpdate(this);
        }
        #endregion

        #region LifeCycle
        public void OnUpdate()
        {
            if (!IsTyping) return;

            _visibleCount += Time.deltaTime / DialogueCharInterval;
            ApplyVisibleCount();
        }

        private void OnDestroy()
        {
            Global.Instance?.UnBindUpdate(this);
        }
        #endregion

        #region Public
        /// <summary>대화창을 열고 대사를 처음부터 한 글자씩 띄운다</summary>
        public void ShowLine(string speaker, string line)
        {
            _panel.SetActive(true);
            _speakerText.text = speaker;
            _lineText.text = line;
            //글자 수는 메시를 만들어야 나오므로 이번 프레임에 바로 만든다
            _lineText.ForceMeshUpdate(ignoreActiveState: true);
            _totalCount = _lineText.textInfo.characterCount;
            _visibleCount = 0f;
            ApplyVisibleCount();
        }

        /// <summary>한 글자씩 나오던 대사를 한 번에 다 보여준다</summary>
        public void CompleteLine()
        {
            _visibleCount = _totalCount;
            ApplyVisibleCount();
        }

        public void Hide()
        {
            _panel.SetActive(false);
            _visibleCount = 0f;
            _totalCount = 0;
        }
        #endregion

        #region Private
        private void ApplyVisibleCount()
        {
            _visibleCount = Mathf.Min(_visibleCount, _totalCount);
            _lineText.maxVisibleCharacters = (int)_visibleCount;
            _nextHint.SetActive(!IsTyping);
        }
        #endregion
    }
}
