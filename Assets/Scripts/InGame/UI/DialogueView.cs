using Common;
using Common.Template.Interface;
using InGame.Object;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Common.GameDefine;

namespace InGame.UI
{
    /// <summary>
    /// 말풍선. 말하는 캐릭터의 머리 위에 이름과 대사를 띄우고 캐릭터를 따라다닌다. 대사는 한 글자씩 드러낸다.
    /// 화면 위에 그리는 캔버스에 두고 매 프레임 캐릭터의 월드 위치를 화면 위치로 바꿔서 옮긴다.
    /// 월드에 두지 않는 이유는 카메라를 확대해도 글자 크기가 같아야 하고, 검은 페이드 화면 위에서도 대사가 보여야 하기 때문이다.
    /// 무엇을 언제 띄울지는 DialoguePlayer가 정하고 여기서는 보여주기만 한다.
    /// 프리팹은 에디터 메뉴 UI > Build Dialogue View가 코드로 조립해서 만든다. 크기와 간격도 거기서 정한다
    /// </summary>
    public class DialogueView : MonoBehaviour, IUpdateable
    {
        #region Fields
        //말풍선 전체. 대화가 없을 때는 꺼둔다. 크기는 안의 글자에 맞춰 레이아웃이 정한다
        [SerializeField] private RectTransform _bubble;
        //말풍선 아래의 꼬리. 말하는 캐릭터를 가리킨다
        [SerializeField] private RectTransform _tail;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _lineText;
        //대사의 폭을 정하는 곳. 짧은 대사는 글자 폭만큼, 긴 대사는 최대 폭에서 줄을 바꾼다
        [SerializeField] private LayoutElement _lineLayout;
        //다음 대사로 넘길 수 있다는 표시. 대사가 다 나온 뒤에만 보인다
        [SerializeField] private TMP_Text _nextHint;

        //아래 값은 캔버스 단위(기준 해상도의 픽셀)다
        //대사 한 줄의 최대 폭
        [SerializeField] private float _maxLineWidth;
        //머리 위에서 말풍선 아랫변까지의 간격. 꼬리가 들어갈 자리다
        [SerializeField] private float _headGap;
        //말풍선이 화면 끝에서 떨어져 있어야 하는 간격
        [SerializeField] private float _screenMargin;
        //꼬리가 말풍선 양 끝에서 떨어져 있어야 하는 간격. 둥근 모서리 밖으로 나가지 않게 한다
        [SerializeField] private float _tailInset;

        //캐릭터의 월드 위치를 화면 위치로 바꿀 카메라
        private Camera _camera;
        private Canvas _canvas;
        private RectTransform _canvasRect;

        //지금 말하는 캐릭터. 없으면(찾지 못했거나 카메라가 없으면) 말풍선을 화면 아래 가운데에 둔다
        private ObjectBase _speaker;
        //화자를 따라가는 대사인지. 따라가던 화자가 도중에 사라진 것과 처음부터 화자가 없던 것을 구분한다
        private bool _hasSpeaker;

        //지금까지 드러낸 글자 수. 소수점까지 세서 프레임 간격과 상관없이 같은 속도로 나오게 한다
        private float _visibleCount;
        //지금 대사의 전체 글자 수. 리치 텍스트 태그는 빼고 센다
        private int _totalCount;

        //대사가 아직 한 글자씩 나오는 중인지
        public bool IsTyping => _visibleCount < _totalCount;
        #endregion

        #region Init
        public void Init(Camera camera)
        {
            _camera = camera;
            _canvas = GetComponent<Canvas>();
            _canvasRect = (RectTransform)transform;

            Hide();
            Global.Instance.BindUpdate(this);
            //말풍선은 카메라가 이번 프레임에 다 움직인 뒤에 옮겨야 한 프레임 늦게 따라오지 않는다.
            //카메라는 LateUpdate에서 움직이는데 Global의 LateUpdate는 묶인 순서에 따라 돌아서 순서를 정해둘 수 없다.
            //그래서 모든 LateUpdate가 끝나고 캔버스를 그리기 직전에 옮긴다
            Canvas.willRenderCanvases += UpdatePosition;
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
            Canvas.willRenderCanvases -= UpdatePosition;
            Global.Instance?.UnBindUpdate(this);
        }
        #endregion

        #region Public
        /// <summary>
        /// 말풍선을 speaker의 머리 위에 띄우고 대사를 처음부터 한 글자씩 드러낸다.
        /// speaker가 없으면 화면 아래 가운데에 꼬리 없이 띄운다 (화자를 찾지 못해도 대사는 읽을 수 있게 한다)
        /// </summary>
        public void ShowLine(ObjectBase speaker, string speakerName, string line)
        {
            _speaker = speaker;
            _hasSpeaker = speaker != null && _camera != null;
            _bubble.gameObject.SetActive(true);
            _tail.gameObject.SetActive(_hasSpeaker);

            _nameText.text = speakerName;
            _lineText.text = line;
            //한 줄로 썼을 때의 폭을 넘겨서, 최대 폭보다 긴 대사만 줄을 바꾼다.
            //소수점 아래가 잘리면 마지막 글자가 다음 줄로 넘어가므로 올림한다
            _lineLayout.preferredWidth = Mathf.Min(Mathf.Ceil(_lineText.GetPreferredValues(line).x), _maxLineWidth);
            //레이아웃은 프레임 끝에 다시 잡힌다. 그때까지 기다리면 아래에서 위치를 정할 때 앞 대사의 크기를 쓰게 된다
            LayoutRebuilder.ForceRebuildLayoutImmediate(_bubble);

            //글자 수는 메시를 만들어야 나오므로 이번 프레임에 바로 만든다
            _lineText.ForceMeshUpdate(ignoreActiveState: true);
            _totalCount = _lineText.textInfo.characterCount;
            _visibleCount = 0f;
            ApplyVisibleCount();
            UpdatePosition();
        }

        /// <summary>한 글자씩 나오던 대사를 한 번에 다 보여준다</summary>
        public void CompleteLine()
        {
            _visibleCount = _totalCount;
            ApplyVisibleCount();
        }

        public void Hide()
        {
            _bubble.gameObject.SetActive(false);
            _speaker = null;
            _hasSpeaker = false;
            _visibleCount = 0f;
            _totalCount = 0;
        }
        #endregion

        #region Private
        private void ApplyVisibleCount()
        {
            _visibleCount = Mathf.Min(_visibleCount, _totalCount);
            _lineText.maxVisibleCharacters = (int)_visibleCount;
            //오브젝트를 끄면 레이아웃에서 빠져서 대사가 다 나오는 순간 말풍선 높이가 바뀐다. 자리는 둔 채 투명하게만 한다
            _nextHint.canvasRenderer.SetAlpha(IsTyping ? 0f : 1f);
        }

        /// <summary>
        /// 말풍선을 화자의 머리 위로 옮긴다. 화면 밖으로 나가지 않게 막고, 막혀서 옆으로 밀리면 꼬리만 화자 쪽으로 옮긴다.
        /// 화자가 없는 대사는 화면 아래 가운데에 둔다
        /// </summary>
        private void UpdatePosition()
        {
            if (!_bubble.gameObject.activeSelf) return;
            //대사를 띄운 뒤에 화자가 사라졌으면(쓰러짐, Despawn) 마지막 자리에 둔다
            if (_hasSpeaker && _speaker == null) return;

            //캔버스의 왼쪽 아래를 0,0으로 보는 캔버스 단위로 계산한다. 화면 픽셀을 캔버스 배율로 나누면 캔버스 단위다
            Vector2 canvasSize = _canvasRect.rect.size;
            Vector2 anchor = _hasSpeaker
                ? (Vector2)_camera.WorldToScreenPoint(_speaker.Top) / _canvas.scaleFactor
                : new Vector2(canvasSize.x * 0.5f, 0f);

            Vector2 size = _bubble.rect.size;
            float halfWidth = size.x * 0.5f;
            float x = Mathf.Clamp(anchor.x, _screenMargin + halfWidth, canvasSize.x - _screenMargin - halfWidth);
            float y = Mathf.Clamp(anchor.y + _headGap, _screenMargin, canvasSize.y - _screenMargin - size.y);
            _bubble.anchoredPosition = new Vector2(x, y);

            float tailX = Mathf.Clamp(anchor.x - x, _tailInset - halfWidth, halfWidth - _tailInset);
            _tail.anchoredPosition = new Vector2(tailX, _tail.anchoredPosition.y);
        }
        #endregion
    }
}
