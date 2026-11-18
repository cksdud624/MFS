using System.Collections.Generic;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.UI
{
    /// <summary>
    /// 월드의 대상이나 지점 위에 띄우는 아이콘 (퀘스트 목표, 시나리오로 이어지는 목표, 일반 대화).
    /// 무엇을 어디에 띄울지는 StoryMarkerPresenter가 매 프레임 정해서 넘기고, 여기서는 그 자리에 아이콘을 놓기만 한다.
    /// 말풍선과 같이 화면 위의 캔버스에 두고 월드 위치를 화면 위치로 바꿔서 옮긴다. 카메라를 확대해도 아이콘 크기가 같다.
    /// 퀘스트와 시나리오 아이콘은 멀리서도 보여야 해서, 대상이 화면 밖이면 화면 가장자리에 붙여 방향을 알려준다.
    /// 프리팹은 에디터 메뉴 UI > Build Marker View가 코드로 조립해서 만든다
    /// </summary>
    public class MarkerView : MonoBehaviour
    {
        #region Types
        /// <summary>띄울 아이콘 하나. worldPosition 바로 위에 아이콘의 아랫변이 온다</summary>
        public readonly struct Marker
        {
            public StoryMarkerKind Kind { get; }
            public Vector2 WorldPosition { get; }

            public Marker(StoryMarkerKind kind, Vector2 worldPosition)
            {
                Kind = kind;
                WorldPosition = worldPosition;
            }
        }
        #endregion

        #region Fields
        //종류별 아이콘의 원본. 꺼둔 채로 두고 필요한 만큼 복사해서 쓴다
        [SerializeField] private RectTransform _questTemplate;
        [SerializeField] private RectTransform _scenarioTemplate;
        [SerializeField] private RectTransform _talkTemplate;
        //아래 값은 캔버스 단위(기준 해상도의 픽셀)다
        //가리키는 곳(머리 위)에서 아이콘 아랫변까지의 간격
        [SerializeField] private float _gap;
        //화면 가장자리에 붙일 때 화면 끝에서 떨어져 있어야 하는 간격
        [SerializeField] private float _screenMargin;

        //월드 위치를 화면 위치로 바꿀 카메라
        private Camera _camera;
        private Canvas _canvas;
        private RectTransform _canvasRect;

        //종류별로 만들어둔 아이콘. 한 번 만들면 켜고 끄면서 계속 재사용한다. 인덱스가 StoryMarkerKind다
        private readonly List<RectTransform>[] _icons = { new(), new(), new() };
        //이번 프레임에 종류별로 쓴 아이콘 수
        private readonly int[] _usedCounts = new int[3];
        //지금 켜져 있는 아이콘과 그 아이콘이 가리키는 곳. 그리기 직전에 자리를 맞춘다
        private readonly List<RectTransform> _activeIcons = new();
        private readonly List<Marker> _activeMarkers = new();
        #endregion

        #region Init
        public void Init(Camera camera)
        {
            _camera = camera;
            _canvas = GetComponent<Canvas>();
            _canvasRect = (RectTransform)transform;

            _questTemplate.gameObject.SetActive(false);
            _scenarioTemplate.gameObject.SetActive(false);
            _talkTemplate.gameObject.SetActive(false);

            //카메라가 이번 프레임에 다 움직인 뒤에 자리를 맞춰야 아이콘이 한 프레임 늦게 따라오지 않는다 (말풍선과 같은 이유)
            Canvas.willRenderCanvases += UpdatePositions;
        }
        #endregion

        #region LifeCycle
        private void OnDestroy() => Canvas.willRenderCanvases -= UpdatePositions;
        #endregion

        #region Public
        /// <summary>
        /// 이번 프레임에 띄울 아이콘을 통째로 넘긴다. 목록에 없는 아이콘은 끈다. 빈 목록을 넘기면 모두 꺼진다.
        /// 아이콘을 켜고 끄는 것은 여기서 바로 하고, 자리는 캔버스를 그리기 직전에 맞춘다
        /// </summary>
        public void Show(IReadOnlyList<Marker> markers)
        {
            for (int kind = 0; kind < _usedCounts.Length; kind++)
                _usedCounts[kind] = 0;
            _activeIcons.Clear();
            _activeMarkers.Clear();

            //카메라가 없으면 화면 위치를 구할 수 없으므로 아무것도 띄우지 않는다
            if (_camera != null)
            {
                for (int i = 0; i < markers.Count; i++)
                {
                    var icon = TakeIcon(markers[i].Kind);
                    _activeIcons.Add(icon);
                    _activeMarkers.Add(markers[i]);
                }
            }

            //이번에 쓰지 않은 아이콘은 끈다
            for (int kind = 0; kind < _icons.Length; kind++)
            {
                for (int i = _usedCounts[kind]; i < _icons[kind].Count; i++)
                    _icons[kind][i].gameObject.SetActive(false);
            }
        }
        #endregion

        #region Private
        /// <summary>그 종류의 아이콘을 하나 꺼내서 켠다. 모자라면 원본을 복사해서 늘린다</summary>
        private RectTransform TakeIcon(StoryMarkerKind kind)
        {
            var icons = _icons[(int)kind];
            int index = _usedCounts[(int)kind]++;
            if (index == icons.Count)
                icons.Add(Instantiate(GetTemplate(kind), _canvasRect));

            var icon = icons[index];
            icon.gameObject.SetActive(true);
            return icon;
        }

        private RectTransform GetTemplate(StoryMarkerKind kind) => kind switch
        {
            StoryMarkerKind.Scenario => _scenarioTemplate,
            StoryMarkerKind.Talk => _talkTemplate,
            _ => _questTemplate,
        };

        /// <summary>
        /// 켜져 있는 아이콘을 가리키는 곳 위로 옮긴다. 캔버스의 왼쪽 아래를 0,0으로 보는 캔버스 단위로 계산한다.
        /// 퀘스트와 시나리오 아이콘은 화면 밖으로 나가지 않게 막아서, 대상이 화면 밖이면 가장자리에 붙는다.
        /// 대화 아이콘은 가까이 있을 때만 뜨는 것이라 막지 않는다
        /// </summary>
        private void UpdatePositions()
        {
            if (_activeIcons.Count == 0 || _camera == null) return;

            Vector2 canvasSize = _canvasRect.rect.size;
            for (int i = 0; i < _activeIcons.Count; i++)
            {
                var icon = _activeIcons[i];
                var marker = _activeMarkers[i];
                Vector2 position = (Vector2)_camera.WorldToScreenPoint(marker.WorldPosition) / _canvas.scaleFactor;
                position.y += _gap;

                if (marker.Kind != StoryMarkerKind.Talk)
                {
                    Vector2 size = icon.rect.size;
                    float halfWidth = size.x * 0.5f;
                    position.x = Mathf.Clamp(position.x, _screenMargin + halfWidth, canvasSize.x - _screenMargin - halfWidth);
                    position.y = Mathf.Clamp(position.y, _screenMargin, canvasSize.y - _screenMargin - size.y);
                }
                icon.anchoredPosition = position;
            }
        }
        #endregion
    }
}
