using Cysharp.Threading.Tasks;
using UnityEngine;

namespace InGame.Component
{
    public class CameraController : MonoBehaviour
    {
        public async UniTask Init()
        {
            await UniTask.CompletedTask;
        }
    }
}