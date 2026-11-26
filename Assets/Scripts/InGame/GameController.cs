using System.Threading;
using Common;
using Common.Scene.Parameter;
using Cysharp.Threading.Tasks;
using Generated.Table;
using InGame.Battle;
using InGame.Context;
using InGame.Object;
using InGame.World;
using UnityEngine;
using static Common.GameDefine;

namespace InGame
{
    /// <summary>
    /// Main 씬의 게임을 준비한다. 지금은 이야기(Quest 표)를 거치지 않고, 들어오자마자 보스전을 벌인다.
    /// 여기서는 맵과 카메라를 만들고 시작 Zone에 플레이어를 세운다. 그 플레이어를 BossBattleController에 넘기면,
    /// 플레이어가 선 Zone이 보스 스테이지가 되고 보스를 세우는 것부터 끝까지는 BossBattleController가 맡는다
    /// </summary>
    public class GameController : MonoBehaviour
    {
        #region Fields
        private InGameContext _inGameContext;
        private ObjectSpawner _objectSpawner;
        //플레이어 주변 Zone만 불러둔다. 매 프레임 갱신 등록을 씬이 내려갈 때 해제한다
        private ZoneStreamer _zoneStreamer;
        //플레이어를 따라가는 카메라. 매 프레임 갱신 등록을 씬이 내려갈 때 해제한다. 메인 카메라가 없으면 null
        private CameraDirector _cameraDirector;
        //보스전. 이벤트 구독과 Zone 잠금, 체력바 프리팹을 씬이 내려갈 때 거둔다
        private BossBattleController _bossBattle;
        #endregion

        #region Init
        public async UniTask Init(SceneParameterMain sceneParameterMain)
        {
            _inGameContext = new();
            _objectSpawner = gameObject.AddComponent<ObjectSpawner>();
            _objectSpawner.Init(_inGameContext);

            var tableManager = Global.Instance.TableManager;
            var cancellationToken = this.GetCancellationTokenOnDestroy();

            //맵은 Zone 프리팹을 격자에 이어 붙여 만든다. 표가 틀리면 에러만 남기고 맞는 Zone으로 진행한다
            ZoneLayout.TryBuild(tableManager.MapRecord.GetAllRecord(), tableManager.ZoneRecord.GetAllRecord(), out var zoneLayout);
            var zoneRoot = new GameObject("Zones").transform;
            _zoneStreamer = new ZoneStreamer(zoneLayout, new PrefabZoneLoader(Global.Instance.AssetManager, zoneRoot),
                _inGameContext, Camera.main, cancellationToken);
            //카메라는 Zone 끝을 넘어 보여주지 않으므로 Zone 배치와 지금 맵을 받는다
            _cameraDirector = CreateCameraDirector(zoneLayout);

            //플레이어와 보스가 선 뒤에 Init을 끝낸다. Init이 끝나면 로딩 화면이 걷히므로, 먼저 끝내면 아무도 없는 맵이 보인다
            var player = await SpawnPlayer(zoneLayout, tableManager, cancellationToken);
            if (player == null) return;

            _bossBattle = new BossBattleController();
            await _bossBattle.Init(_inGameContext, zoneLayout, _zoneStreamer);
            await _bossBattle.StartStage(player, _objectSpawner, tableManager);
        }
        #endregion

        #region LifeCycle
        private void OnDestroy()
        {
            //잠근 Zone을 풀어야 하므로 Zone보다 먼저 거둔다
            _bossBattle?.Dispose();
            _zoneStreamer?.Dispose();
            _cameraDirector?.Dispose();
        }
        #endregion

        #region Private
        /// <summary>
        /// 시작 Zone(PlayerStartZoneId)에 플레이어를 세운다. 그 Zone을 다 불러온 뒤에 세운다. 바닥이 생기기 전에 세우면 떨어진다.
        /// Zone이 없거나 세우지 못하면 null
        /// </summary>
        private async UniTask<ObjectBase> SpawnPlayer(ZoneLayout zoneLayout, TableManager tableManager, CancellationToken cancellationToken)
        {
            if (zoneLayout == null || !zoneLayout.HasZone(PlayerStartZoneId))
            {
                Debug.LogError($"Player start zone {PlayerStartZoneId} is not defined in Zone table.");
                return null;
            }

            //플레이어가 서기 전에는 컬링 기준이 없어서 고정해야 올라온다. 고정은 풀지 않는다.
            //풀면 플레이어를 기준으로 한 컬링이 돌기 전에(기준 Zone이 없는 채로) 그 Zone이 내려간다
            await _zoneStreamer.Pin(new[] { PlayerStartZoneId }, cancellationToken);
            await _objectSpawner.SpawnPlayer(tableManager.ObjectRecord.GetRecord(PlayerObjectId),
                zoneLayout.ToWorld(PlayerStartZoneId, PlayerStartPosition));

            if (_inGameContext.Player == null)
                Debug.LogError($"Player ({PlayerObjectId}) could not be spawned.");
            return _inGameContext.Player;
        }

        private CameraDirector CreateCameraDirector(ZoneLayout zoneLayout)
        {
            var camera = Camera.main;
            if (camera != null) return new CameraDirector(camera, _inGameContext, zoneLayout, _zoneStreamer);

            Debug.LogError("Main camera is missing. Tag the scene camera as MainCamera.");
            return null;
        }
        #endregion
    }
}
