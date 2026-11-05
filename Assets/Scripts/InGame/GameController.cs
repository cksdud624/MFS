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
        #region Fields
        private InGameContext _inGameContext;
        private ObjectSpawner _objectSpawner;
        #endregion

        #region Init
        public async UniTask Init(SceneParameterMain sceneParameterMain)
        {
            _inGameContext = new();
            _objectSpawner = gameObject.AddComponent<ObjectSpawner>();
            _objectSpawner.Init(_inGameContext);

            //플레이어 스폰
            await _objectSpawner.SpawnPlayer(Global.Instance.TableManager.ObjectRecord.GetRecord(1001), new Vector2(0, 0.5f));

            //테스트용 적 스폰 (적 테이블이 생기면 해당 Id로 교체)
            await _objectSpawner.SpawnObject(Global.Instance.TableManager.ObjectRecord.GetRecord(1001), new Vector2(3f, 0.5f));

            LoadCompleted();
        }
        #endregion

        #region Private
        private void LoadCompleted()
        {
            _inGameContext.Player.AttachController();
        }
        #endregion
    }
}
