using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace InGame.UI
{
    /// <summary>
    /// 화면 전체를 덮는 페이드. 덮개의 투명도를 시간에 맞춰 바꾼다.
    /// 프리팹은 에디터 메뉴 UI > Build Screen Fade View가 코드로 조립해서 만든다
    /// </summary>
    public class ScreenFadeView : MonoBehaviour
    {
        #region Fields
        //화면 전체를 덮는 검은 이미지
        [SerializeField] private Image _cover;

        //지금 화면을 덮고 있는지. 다 걷힌 상태가 아니면 덮인 것으로 본다
        public bool IsCovered => _cover.color.a > 0f;
        #endregion

        #region Init
        public void Init() => SetAlpha(0f);
        #endregion

        #region Public
        /// <summary>
        /// 덮개의 투명도를 alpha까지 바꾼다. 1이면 화면이 다 가려지고(페이드 아웃) 0이면 다 보인다(페이드 인).
        /// duration이 0이면 바로 바꾼다
        /// </summary>
        public async UniTask Fade(float alpha, float duration, CancellationToken cancellationToken)
        {
            float from = _cover.color.a;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                SetAlpha(Mathf.Lerp(from, alpha, Mathf.SmoothStep(0f, 1f, elapsed / duration)));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                elapsed += Time.deltaTime;
            }
            SetAlpha(alpha);
        }
        #endregion

        #region Private
        private void SetAlpha(float alpha)
        {
            var color = _cover.color;
            color.a = alpha;
            _cover.color = color;
            //다 걷혔을 때는 그리지 않는다. 투명한 전체 화면 이미지도 매 프레임 그리는 비용이 든다
            _cover.enabled = alpha > 0f;
        }
        #endregion
    }
}
