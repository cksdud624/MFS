using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Common.Scene
{
    /// <summary>
    /// 씬마다 하나씩 두는 시작점. 씬이 올라오면 Awake에서 SceneLoader.SetCurrentScene으로 자기를 알린다
    /// </summary>
    public class SceneLoadBase : MonoBehaviour
    {
        /// <summary>
        /// 씬을 준비한다. 끝날 때까지 로딩 화면이 덮고 있으므로, 화면에 보여도 되는 상태가 되었을 때 끝낸다
        /// </summary>
        public virtual UniTask InitScene() => UniTask.CompletedTask;

        public virtual void DisposeScene()
        {

        }
    }
}
