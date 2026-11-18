using Common;
using Common.Scene;
using Common.Scene.Parameter;
using Common.Template.Interface;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace InGame
{
    public class SceneLoadMain : SceneLoadBase, ISceneParameter<SceneParameterMain>
    {
        #region Fields
        [SerializeField] private GameController gameController;

        private SceneParameterMain _sceneParameterMain;
        #endregion

        #region LifeCycle
        protected void Awake() => Global.Instance.SceneLoader.SetCurrentScene<SceneParameterMain>(this);
        #endregion

        #region Public
        public override async UniTask InitScene()
        {
            if (_sceneParameterMain == null)
            {
                Debug.LogError($"{typeof(SceneParameterMain)} is null");
                return;
            }

            await gameController.Init(_sceneParameterMain);
        }

        public void SetParameter(SceneParameterMain parameter) => _sceneParameterMain = parameter;
        #endregion
    }
}
