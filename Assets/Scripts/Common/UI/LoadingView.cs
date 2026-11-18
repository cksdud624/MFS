using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using static Common.GameDefine;

namespace Common.UI
{
    /// <summary>
    /// 씬을 넘어갈 때 화면 전체를 덮는 로딩 화면. 새 씬이 준비를 마칠 때까지 덮고 있다가 서서히 걷힌다.
    /// 씬이 바뀌어도 남아 있어야 해서 SceneLoader가 갖고 있고, 언제 덮고 걷을지도 SceneLoader가 정한다.
    /// 프리팹은 에디터 메뉴 UI > Build Loading View가 코드로 조립해서 만든다
    /// </summary>
    public class LoadingView : MonoBehaviour
    {
        #region Fields
        //화면 전체의 투명도. 걷힐 때 줄인다
        [SerializeField] private CanvasGroup _group;
        //로딩 글. 끝에 점이 붙어 있다 ("로딩 중...")
        [SerializeField] private TMP_Text _text;
        //글 끝에 붙은 점의 수
        [SerializeField] private int _dotCount;

        //덮고 있는지. 걷히는 중에는 false다
        private bool _isShown;
        //지금 보이는 점의 수. 바뀔 때만 글자를 다시 그린다
        private int _visibleDots = -1;
        #endregion

        #region Init
        public void Init() => gameObject.SetActive(false);
        #endregion

        #region LifeCycle
        /// <summary>
        /// 글 끝의 점을 하나씩 보였다가 다시 처음으로 돌린다.
        /// 글자를 바꾸지 않고 보이는 글자 수만 바꾼다. 글자를 바꾸면 폭이 달라져서 글이 좌우로 흔들린다
        /// </summary>
        private void Update()
        {
            //게임 시간이 멈춰 있어도 움직이게 실제 시간으로 센다
            int dots = (int)(Time.unscaledTime / LoadingDotInterval) % (_dotCount + 1);
            if (dots == _visibleDots) return;

            _visibleDots = dots;
            _text.maxVisibleCharacters = _text.text.Length - _dotCount + dots;
        }
        #endregion

        #region Public
        /// <summary>화면을 바로 덮는다. 걷히는 중이었으면 걷던 것을 그만둔다</summary>
        public void Show()
        {
            _isShown = true;
            _group.alpha = 1f;
            gameObject.SetActive(true);
        }

        /// <summary>화면을 서서히 걷는다. 덮고 있지 않으면 아무것도 하지 않는다</summary>
        public async UniTask Hide()
        {
            if (!_isShown) return;
            _isShown = false;

            var cancellationToken = this.GetCancellationTokenOnDestroy();
            float elapsed = 0f;
            while (elapsed < LoadingFadeDuration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                //걷히는 도중에 다시 덮었으면(바로 다른 씬으로 넘어감) 그만둔다
                if (_isShown) return;

                elapsed += Time.unscaledDeltaTime;
                _group.alpha = 1f - Mathf.Clamp01(elapsed / LoadingFadeDuration);
            }
            gameObject.SetActive(false);
        }
        #endregion
    }
}
