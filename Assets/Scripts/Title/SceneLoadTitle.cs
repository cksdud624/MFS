using Common;
using Common.Scene;
using Cysharp.Threading.Tasks;

namespace Title
{
    /// <summary>
    /// 타이틀 씬의 시작점. 씬이 올라오면 SceneLoader에 자기를 알리고 TitleController를 시작한다
    /// </summary>
    public class SceneLoadTitle : SceneLoadBase
    {
        #region Fields
        private TitleController _titleController;
        #endregion

        #region LifeCycle
        protected void Awake() => Global.Instance.SceneLoader.SetCurrentScene(this);
        #endregion

        #region Public
        public override UniTask InitScene()
        {
            if (!TryGetComponent(out _titleController))
                _titleController = gameObject.AddComponent<TitleController>();
            return _titleController.Init();
        }
        #endregion
    }
}
