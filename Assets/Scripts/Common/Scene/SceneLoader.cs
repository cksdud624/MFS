using Common.Template.Interface;
using Common.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Common.AssetKeys;
using static Common.GameDefine;

namespace Common.Scene
{
    /// <summary>
    /// 씬을 넘기고, 넘어가는 동안 로딩 화면으로 덮는다.
    /// 새 씬은 올라오면 자기 시작점(SceneLoadBase)을 SetCurrentScene으로 알리고, 그 씬이 준비(InitScene)를 마치면 로딩 화면을 걷는다
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        #region Fields
        private SceneType _sceneType = SceneType.BootStrap;
        private SceneLoadBase _currentScene;

        private object _sceneParameter;
        //씬을 넘어가는 동안 화면을 덮는다. 프리팹이 없으면 null이고 덮지 않은 채로 넘어간다
        private LoadingView _loadingView;
        #endregion

        #region Init
        /// <summary>
        /// 로딩 화면을 만들어서 바로 덮는다. 게임이 켜진 직후의 준비(표 읽기)도 덮으려고 다른 준비보다 먼저 부른다.
        /// 로딩 화면은 씬이 바뀌어도 남도록 이 오브젝트 아래에 두고, 게임이 꺼질 때까지 쓰므로 프리팹을 반납하지 않는다
        /// </summary>
        public async UniTask Init(AssetManager assetManager)
        {
            var prefab = await assetManager.LoadAssetAsync<GameObject>(LoadTarget.UI, UILoadingView);
            if (prefab == null)
            {
                Debug.LogError($"{UILoadingView} is missing. Build it with the editor menu UI > Build All UI.");
                return;
            }

            if (!Instantiate(prefab, transform).TryGetComponent(out _loadingView))
            {
                Debug.LogError($"{UILoadingView} has no {nameof(LoadingView)}.");
                return;
            }
            _loadingView.Init();
            _loadingView.Show();
        }
        #endregion

        #region Public
        public void LoadScene(SceneType sceneType) => Load(sceneType, null);

        public void LoadScene<T>(SceneType sceneType, T sceneParameter) => Load(sceneType, sceneParameter);

        public void SetCurrentScene<T>(SceneLoadBase sceneLoadBase) where T : class
        {
            if (_sceneParameter is not T parameter)
            {
                Debug.Log($"Scene parameter type is not {typeof(T)} => {_sceneParameter?.GetType()}");
                return;
            }
            _currentScene?.DisposeScene();
            _currentScene = sceneLoadBase;
            (_currentScene as ISceneParameter<T>)?.SetParameter(parameter);
            InitCurrentScene().Forget();
        }

        public void SetCurrentScene(SceneLoadBase sceneLoadBase)
        {
            _currentScene?.DisposeScene();
            _currentScene = sceneLoadBase;
            InitCurrentScene().Forget();
        }
        #endregion

        #region Private
        private void Load(SceneType sceneType, object sceneParameter)
        {
            if (_sceneType == sceneType)
            {
                Debug.Log($"{sceneType} scene is already loaded");
                return;
            }
            _sceneType = sceneType;
            _sceneParameter = sceneParameter;

            //새 씬이 준비를 마칠 때까지 덮어둔다. 걷는 것은 InitCurrentScene이 한다
            if (_loadingView != null)
                _loadingView.Show();
            SceneManager.LoadScene(_sceneType.ToString(), LoadSceneMode.Single);
            Debug.Log($"{sceneType} scene is loaded");
        }

        /// <summary>
        /// 새 씬의 준비를 기다렸다가 로딩 화면을 걷는다.
        /// 씬 파일을 여는 것은 금방 끝나고, 시간이 걸리는 것은 그 뒤의 준비(에셋과 Zone 불러오기)라서 여기까지 덮는다
        /// </summary>
        private async UniTask InitCurrentScene()
        {
            var scene = _currentScene;
            await scene.InitScene();

            //준비하는 사이에 다른 씬으로 넘어갔으면 걷지 않는다. 그 씬이 준비를 마쳤을 때 걷는다
            if (scene != _currentScene || _loadingView == null) return;
            await _loadingView.Hide();
        }
        #endregion
    }
}
