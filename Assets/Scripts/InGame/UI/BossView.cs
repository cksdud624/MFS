using TMPro;
using UnityEngine;

namespace InGame.UI
{
    /// <summary>
    /// 화면 위쪽 가운데의 보스 체력바. 보스의 이름과 지금 체력바가 얼마나 남았는지, 몇 번째 페이즈인지를 띄우고,
    /// 그 아래에 보스가 지금 내는 패턴의 이름을 띄운다 (확인용).
    /// 무엇을 언제 띄울지는 StoryHudPresenter가 매 프레임 정하고 여기서는 보여주기만 한다.
    /// 프리팹은 에디터 메뉴 UI > Build Boss View가 코드로 조립해서 만든다
    /// </summary>
    public class BossView : MonoBehaviour
    {
        #region Fields
        //페이즈 글의 모양 (지금 페이즈 / 전체 페이즈 수)
        private const string PhaseFormat = "페이즈 {0} / {1}";

        //체력바 전체. 보스전이 아닐 때는 꺼둔다
        [SerializeField] private GameObject _panel;
        //보스의 이름 (Actor 표의 Name)
        [SerializeField] private TMP_Text _nameText;
        //남은 Hp만큼 채워지는 막대. 왼쪽에 붙어 있고, 오른쪽 끝을 남은 비율의 자리로 옮긴다
        [SerializeField] private RectTransform _hpFill;
        //몇 번째 페이즈인지. 체력바가 하나뿐인 보스는 비워둔다
        [SerializeField] private TMP_Text _phaseText;
        //보스가 지금 내는 패턴의 이름. 패턴이 제대로 나오는지 보려고 막대 아래에 띄우는 확인용 글이다
        [SerializeField] private TMP_Text _patternText;

        //마지막으로 띄운 값. 매 프레임 같은 값으로 불리므로 바뀌었을 때만 고친다
        private int _hp = -1;
        private int _maxHp = -1;
        private int _phase = -1;
        private int _phaseCount = -1;
        #endregion

        #region Init
        public void Init() => Hide();
        #endregion

        #region Public
        /// <summary>
        /// 체력바를 띄운다. phase는 0부터 세는 지금 페이즈이고, patternName은 보스가 지금 내는 패턴의 이름이다 (없으면 빈 문자열).
        /// 글자는 넣을 때마다 메시와 레이아웃을 다시 만들므로 내용이 바뀌었을 때만 넣는다
        /// </summary>
        public void Show(string bossName, int hp, int maxHp, int phase, int phaseCount, string patternName)
        {
            if (!_panel.activeSelf)
                _panel.SetActive(true);
            if (_nameText.text != bossName)
                _nameText.text = bossName;
            if (_patternText.text != patternName)
                _patternText.text = patternName;

            if (_hp != hp || _maxHp != maxHp)
            {
                _hp = hp;
                _maxHp = maxHp;
                float ratio = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
                _hpFill.anchorMax = new Vector2(ratio, 1f);
            }

            if (_phase != phase || _phaseCount != phaseCount)
            {
                _phase = phase;
                _phaseCount = phaseCount;
                _phaseText.text = phaseCount > 1 ? string.Format(PhaseFormat, phase + 1, phaseCount) : string.Empty;
            }
        }

        public void Hide()
        {
            if (_panel.activeSelf)
                _panel.SetActive(false);
        }
        #endregion
    }
}
