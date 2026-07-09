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
        [SerializeField] private GameController gameController;
        
        private SceneParameterMain _sceneParameterMain;
        protected void Awake() => Global.Instance.SceneLoader.SetCurrentScene<SceneParameterMain>(this);
        
        public override void InitScene()
        {
            if (_sceneParameterMain == null)
            {
                Debug.LogError($"{typeof(SceneParameterMain)} is null");
                return;
            }
            
            gameController.Init(_sceneParameterMain).Forget();
        }

        public void SetParameter(SceneParameterMain parameter) => _sceneParameterMain = parameter;
    }
}
