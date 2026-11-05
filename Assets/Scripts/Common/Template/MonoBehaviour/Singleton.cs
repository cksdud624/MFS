using UnityEngine;

namespace Common.Template
{
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        #region Fields
        private static T _instance;
        private static bool _isQuitting;

        public static T Instance
        {
            get
            {
                if (_isQuitting) return null;
                if (_instance == null)
                {
                    GameObject singleton = new GameObject(typeof(T).Name);
                    _instance = singleton.AddComponent<T>();
                    DontDestroyOnLoad(singleton);
                }
                return _instance;
            }
        }
        #endregion

        #region LifeCycle
        protected virtual void OnApplicationQuit() => _isQuitting = true;
        #endregion
    }
}
