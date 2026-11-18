using Common;
using Common.Scene.Parameter;
using Cysharp.Threading.Tasks;
using Title.UI;
using UnityEngine;
using static Common.AssetKeys;
using static Common.GameDefine;

namespace Title
{
    /// <summary>
    /// 타이틀 씬의 흐름. 타이틀 화면을 띄우고, 고른 메뉴에 따라 게임을 시작하거나 끝낸다
    /// </summary>
    public class TitleController : MonoBehaviour
    {
        #region Fields
        //타이틀 화면. 프리팹이 없으면 null
        private TitleView _titleView;
        //씬이 내려갈 때 프리팹을 반납해야 하는지. 인스턴스가 먼저 파괴될 수 있으므로 인스턴스로 판단하지 않는다
        private bool _isViewLoaded;
        #endregion

        #region Init
        public async UniTask Init()
        {
            var prefab = await Global.Instance.AssetManager.LoadAssetAsync<GameObject>(LoadTarget.UI, UITitleView);
            if (prefab == null)
            {
                Debug.LogError($"{UITitleView} is missing. Build it with the editor menu UI > Build All UI.");
                return;
            }
            _isViewLoaded = true;

            if (!Instantiate(prefab).TryGetComponent(out _titleView))
            {
                Debug.LogError($"{UITitleView} has no {nameof(TitleView)}.");
                return;
            }
            _titleView.Init();

            _titleView.OnStartRequested += HandleStartRequested;
            _titleView.OnQuitRequested += HandleQuitRequested;
        }
        #endregion

        #region LifeCycle
        private void OnDestroy()
        {
            //씬이 내려가며 화면이 먼저 파괴되었을 수 있다
            if (_titleView != null)
            {
                _titleView.OnStartRequested -= HandleStartRequested;
                _titleView.OnQuitRequested -= HandleQuitRequested;
            }

            if (!_isViewLoaded) return;
            var assetManager = Global.Instance?.AssetManager;
            if (assetManager != null)
                assetManager.ReleaseAsset<GameObject>(LoadTarget.UI, UITitleView);
        }
        #endregion

        #region Event Handlers
        //새 게임. 이야기를 첫 퀘스트부터 시작한다
        private void HandleStartRequested()
            => Global.Instance.SceneLoader.LoadScene(SceneType.Main, new SceneParameterMain());

        private void HandleQuitRequested()
        {
#if UNITY_EDITOR
            //에디터에서는 Application.Quit이 아무 일도 하지 않으므로 Play를 멈춘다
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        #endregion
    }
}
