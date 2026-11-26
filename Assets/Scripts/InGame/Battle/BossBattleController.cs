using System;
using Common;
using Common.Template.Interface;
using Cysharp.Threading.Tasks;
using Generated.Table;
using InGame.Component.Controller;
using InGame.Component.Controller.Boss;
using InGame.Context;
using InGame.Object;
using InGame.UI;
using InGame.World;
using UnityEngine;
using static Common.AssetKeys;
using static Common.GameDefine;

namespace InGame.Battle
{
    /// <summary>
    /// 보스전을 맡는다. 보스와 관련된 것은 모두 여기를 거친다.
    /// - 스테이지 : 세워진 플레이어를 넘겨받아, 플레이어가 선 Zone을 보스 스테이지로 삼고 그 Zone에 보스를 세운다 (StartStage. Main에 들어오자마자 벌이는 보스전)
    /// - 시작 : 보스에게 보스 AI(ControllerBossAI)를 붙이고(일반 AI였으면 바꾸고) BossFactory가 표로 만든 설계(BossDefinition)를 넣은 뒤,
    ///          보스가 서 있는 Zone을 잠그고 깨운다 (Begin. 이야기의 Boss 명령도 이것을 부른다)
    /// - 진행 : 싸우는 동안 화면 위쪽에 보스의 이름과 체력바, 페이즈를 띄운다 (BossView)
    /// - 끝 : 보스가 쓰러지면 Zone 잠금을 풀고 체력바를 숨긴다
    /// </summary>
    public class BossBattleController : ILateUpdateable, IDisposable
    {
        #region Fields
        private InGameContext _inGameContext;
        private ZoneLayout _zoneLayout;
        private ZoneStreamer _zoneStreamer;
        //보스 체력바. 프리팹이 없으면 null이고, 체력바 없이 싸운다
        private BossView _bossView;
        //체력바 프리팹을 받았는지. 받았으면 끝날 때 반납한다. 인스턴스는 씬과 같이 먼저 파괴될 수 있어서 인스턴스로 판단하지 않는다
        private bool _isBossViewLoaded;

        //싸우는 Zone. 싸우는 동안 잠가서 다른 Zone으로 나가지 못하게 하고, 카메라도 이 Zone 밖을 보여주지 않는다.
        //보스가 어느 Zone에도 서 있지 않으면 null이고, 잠그지 않고 싸운다
        public ZoneLayout.ZoneEntry Zone { get; private set; }
        public ObjectBase Boss { get; private set; }
        //체력바에 띄우는 보스의 이름
        public string BossName { get; private set; }
        //싸우는 중인지. Begin부터 End까지다
        public bool IsRunning { get; private set; }
        //보스와 싸우는 플레이어. StartStage가 넘겨받는다. 이야기의 보스전처럼 넘겨받지 않았으면 월드의 플레이어다
        public ObjectBase Player
        {
            get => _player != null ? _player : _inGameContext.Player;
            private set => _player = value;
        }
        private ObjectBase _player;
        #endregion

        #region Init
        public async UniTask Init(InGameContext inGameContext, ZoneLayout zoneLayout, ZoneStreamer zoneStreamer)
        {
            _inGameContext = inGameContext;
            _zoneLayout = zoneLayout;
            _zoneStreamer = zoneStreamer;

            _bossView = await CreateBossView();

            //쓰러진 캐릭터는 쓰러지는 모션이 끝난 뒤에 알려온다. 보스가 쓰러지면 전투를 끝낸다
            _inGameContext.OnObjectDefeated += HandleObjectDefeated;
            Global.Instance.BindLateUpdate(this);
        }
        #endregion

        #region LifeCycle
        //보스의 Hp는 이번 프레임의 전투가 끝난 뒤에 읽는다
        public void OnLateUpdate()
        {
            //씬이 내려가며 뷰가 먼저 파괴되었을 수 있다
            if (_bossView == null) return;

            if (IsRunning && Boss != null)
                _bossView.Show(BossName, Boss.Hp, Boss.MaxHp, Boss.Phase, Boss.PhaseCount, Boss.PatternName);
            else
                _bossView.Hide();
        }
        #endregion

        #region Public
        /// <summary>
        /// 플레이어가 지금 서 있는 Zone을 보스 스테이지로 삼아 보스전을 시작한다. 플레이어를 넘겨받고, 그 Zone에 보스를 세운 뒤 Begin한다.
        /// 플레이어가 선 Zone은 이미 올라와 있으므로 보스를 바로 세워도 바닥이 있다. 플레이어가 Zone 밖이거나 보스를 세우지 못하면 false
        /// </summary>
        public async UniTask<bool> StartStage(ObjectBase player, ObjectSpawner objectSpawner, TableManager tableManager)
        {
            if (player == null) return false;
            if (_zoneLayout == null || _zoneStreamer == null
                || !_zoneLayout.TryFindZone(_zoneStreamer.MapId, player.transform.position, out var stage))
            {
                Debug.LogError("The player stands outside of any zone. There is no zone to be the boss stage.");
                return false;
            }

            Player = player;
            var boss = await objectSpawner.SpawnEnemy(tableManager.ObjectRecord.GetRecord(BossBattleBossObjectId),
                _zoneLayout.ToWorld(stage.Id, BossBattleBossPosition));
            if (boss == null)
            {
                Debug.LogError($"Boss battle could not spawn the boss ({BossBattleBossObjectId}).");
                return false;
            }
            return Begin(boss, BossBattleBossName);
        }

        /// <summary>
        /// 플레이어가 보스전이 벌어질 Zone(보스가 서 있는 Zone) 안으로 들어왔는지. 경계에서 BossArenaEnterMargin만큼 안쪽이어야 한다.
        /// 경계에 걸친 채 잠그면 막는 벽이 몸 위에 선다. 보스가 Zone 밖에 서 있으면 들어올 Zone이 없으므로, 가까이 다가온 것으로 대신한다 (말 걸기 거리)
        /// </summary>
        public bool IsPlayerInArena(ObjectBase boss)
        {
            var player = Player;
            if (player == null || boss == null) return false;

            Vector2 position = player.transform.position;
            if (!TryFindZone(boss, out var zone))
                return Mathf.Abs(position.x - boss.transform.position.x) <= InteractDistance;

            var rect = zone.Rect;
            return position.x >= rect.xMin + BossArenaEnterMargin && position.x <= rect.xMax - BossArenaEnterMargin
                   && position.y >= rect.yMin && position.y <= rect.yMax;
        }

        /// <summary>
        /// 전투를 시작한다. 보스의 컨트롤러를 보스 AI(ControllerBossAI)로 붙이거나 바꾸고, 표에서 만든 그 보스의 설계(BossDefinition)를 넣은 뒤
        /// 보스가 서 있는 Zone을 잠그고 깨운다. bossName은 체력바에 띄우는 이름이다.
        /// Character 표의 Boss 칸 이름이 Boss 표에 없거나 보스의 표가 틀렸으면 에러를 남기고 false를 돌려준다. 이미 싸우는 중이면 false
        /// </summary>
        public bool Begin(ObjectBase boss, string bossName)
        {
            if (IsRunning || boss == null) return false;

            //설계를 먼저 만든다. 만들지 못하면 컨트롤러를 바꾸지 않아서 그 오브젝트는 하던 대로(일반 AI) 움직인다.
            //틀린 표는 BossFactory가 줄마다 에러로 남긴다
            var tableManager = Global.Instance.TableManager;
            var bossObject = tableManager.ObjectRecord.GetRecord(boss.ObjectId);
            if (!BossFactory.TryCreate(tableManager, bossObject, boss.BossName, out var definition))
            {
                Debug.LogError($"Object {boss.ObjectId} : Boss '{boss.BossName}' could not be built. Check the Boss column of Character table and Boss, BossPattern, BossCommand tables.");
                return false;
            }

            //스폰할 때는 일반 AI가 붙어 있다. 보스 AI로 바꾸고 설계를 넣은 뒤에 깨운다. 설계 없이 깨우면 무엇을 할지 모른다
            var bossAI = boss.ReplaceController<ControllerBossAI>();
            bossAI.SetDefinition(definition);

            //Zone 밖에 서 있으면 잠글 Zone이 없다. 구역 없이 그 자리를 중심으로 싸우게 한다
            bool inZone = TryFindZone(boss, out var zone);
            bossAI.Engage(inZone ? zone.Rect : new Rect(boss.transform.position, Vector2.zero));
            if (inZone)
                _zoneStreamer.Lock(zone.Id);
            else
                Debug.LogWarning($"Boss {boss.ObjectId} stands outside of any zone. The boss battle starts without locking a zone.");

            Zone = inZone ? zone : null;
            Boss = boss;
            BossName = bossName ?? string.Empty;
            IsRunning = true;
            Debug.Log($"[보스전] 시작 : {BossName} (Zone {(inZone ? zone.Id : 0)})");
            return true;
        }

        /// <summary>전투를 끝낸다. 잠근 Zone을 풀어서 다시 옆 Zone으로 갈 수 있게 하고 체력바를 숨긴다. 싸우는 중이 아니면 아무것도 하지 않는다</summary>
        public void End()
        {
            if (!IsRunning) return;

            IsRunning = false;
            if (Zone != null)
                _zoneStreamer.Unlock();
            Zone = null;
            Boss = null;
            Debug.Log("[보스전] 끝");
        }

        public void Dispose()
        {
            End();
            if (_inGameContext != null)
                _inGameContext.OnObjectDefeated -= HandleObjectDefeated;
            Global.Instance?.UnBindLateUpdate(this);
            if (!_isBossViewLoaded) return;

            _isBossViewLoaded = false;
            Global.Instance?.AssetManager?.ReleaseAsset<GameObject>(LoadTarget.UI, UIBossView);
        }
        #endregion

        #region Event Handlers
        //스포너가 먼저 받아서 없앤 뒤에 올 수 있다. 파괴된 오브젝트끼리 비교하면 Unity의 == 가 둘 다 null로 보므로 참조로 비교한다
        private void HandleObjectDefeated(ObjectBase target)
        {
            if (!IsRunning || !ReferenceEquals(target, Boss)) return;

            Debug.Log("[보스전] 보스 쓰러짐");
            End();
        }
        #endregion

        #region Private
        //보스가 지금 서 있는 Zone. 보스전은 이 Zone에서 벌어진다
        private bool TryFindZone(ObjectBase boss, out ZoneLayout.ZoneEntry zone)
        {
            zone = null;
            return _zoneLayout != null && _zoneStreamer != null && _zoneLayout.TryFindZone(_zoneStreamer.MapId, boss.transform.position, out zone);
        }

        /// <summary>보스 체력바 프리팹을 받아 띄운다. 프리팹이 없어도 전투는 확인할 수 있어야 하므로 에러만 남기고 null을 돌려준다</summary>
        private async UniTask<BossView> CreateBossView()
        {
            var prefab = await Global.Instance.AssetManager.LoadAssetAsync<GameObject>(LoadTarget.UI, UIBossView);
            if (prefab == null)
            {
                Debug.LogError($"{UIBossView} is missing. Build it with the editor menu UI > Build All UI.");
                return null;
            }
            _isBossViewLoaded = true;

            if (UnityEngine.Object.Instantiate(prefab).TryGetComponent(out BossView view))
            {
                view.Init();
                return view;
            }

            Debug.LogError($"{UIBossView} has no {nameof(BossView)}.");
            return null;
        }
        #endregion
    }
}
