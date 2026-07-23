using Common;
using Common.Scene.Parameter;
using Cysharp.Threading.Tasks;
using InGame.Context;
using InGame.Object;
using UnityEngine;

namespace InGame
{
    public class GameController : MonoBehaviour
    {
        private InGameContext _inGameContext;
        private ObjectSpawner _objectSpawner;
        
        public async UniTask Init(SceneParameterMain sceneParameterMain)
        {
            _inGameContext = new();
            _objectSpawner = gameObject.AddComponent<ObjectSpawner>();
            await _objectSpawner.Init(_inGameContext);
            
            //플레이어 스폰
            await _objectSpawner.SpawnPlayer(Global.Instance.TableManager.ObjectRecord.GetRecord(1001), new Vector2(0, 0.5f));
            
            LoadCompleted();
        }

        private void LoadCompleted()
        {
            _inGameContext.Player.AttachController();
        }
    }
}
