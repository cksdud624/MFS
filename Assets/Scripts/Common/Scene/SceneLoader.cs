using Common.Template.Interface;
using UnityEngine;
using UnityEngine.SceneManagement;
using SceneType = Common.GameDefine.SceneType;

namespace Common.Scene
{
    public class SceneLoader : MonoBehaviour
    {
        #region Fields
        private SceneType _sceneType = SceneType.BootStrap;
        private SceneLoadBase _currentScene;

        private object _sceneParameter;
        #endregion

        #region Public
        public void LoadScene(SceneType sceneType)
        {
            if (_sceneType == sceneType)
            {
                Debug.Log($"{sceneType} scene is already loaded");
                return;
            }
            _sceneType = sceneType;
            _sceneParameter = null;
            SceneManager.LoadScene(_sceneType.ToString(), LoadSceneMode.Single);
            Debug.Log($"{sceneType} scene is loaded");
        }

        public void LoadScene<T>(SceneType sceneType, T sceneParameter)
        {
            if (_sceneType == sceneType)
            {
                Debug.Log($"{sceneType} scene is already loaded");
                return;
            }
            _sceneType = sceneType;
            _sceneParameter = sceneParameter;
            SceneManager.LoadScene(_sceneType.ToString(), LoadSceneMode.Single);
            Debug.Log($"{sceneType} scene is loaded");
        }

        public void SetCurrentScene<T>(SceneLoadBase sceneLoadBase) where T : class
        {
            if (_sceneParameter is not T parameter)
            {
                Debug.Log($"Scene parameter type is not {typeof(T)} => {_sceneParameter.GetType()}");
                return;
            }
            _currentScene?.DisposeScene();
            _currentScene = sceneLoadBase;
            (_currentScene as ISceneParameter<T>)?.SetParameter(parameter);
            _currentScene?.InitScene();
        }
        public void SetCurrentScene(SceneLoadBase sceneLoadBase)
        {
            _currentScene?.DisposeScene();
            _currentScene = sceneLoadBase;
            _currentScene?.InitScene();
        }
        #endregion
    }
}