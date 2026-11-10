using System.Collections.Generic;
using Common;
using Common.Scene.Parameter;
using Cysharp.Threading.Tasks;
using InGame.Context;
using InGame.Object;
using InGame.Story;
using InGame.UI;
using InGame.World;
using UnityEngine;
using static Common.AssetKeys;
using static Common.GameDefine;

namespace InGame
{
    public class GameController : MonoBehaviour
    {
        #region Fields
        private InGameContext _inGameContext;
        private ObjectSpawner _objectSpawner;
        //씬이 내려갈 때 반납할 UI 프리팹 이름. 인스턴스가 먼저 파괴될 수 있으므로 인스턴스가 아니라 받아둔 이름으로 반납한다
        private readonly List<string> _loadedViews = new();
        //퀘스트 밖에서 Npc에게 말을 걸면 대화를 띄운다. Interact 입력 구독을 씬이 내려갈 때 해제한다
        private TalkPlayer _talkPlayer;
        //플레이어 주변 Zone만 불러둔다. 매 프레임 갱신 등록을 씬이 내려갈 때 해제한다
        private ZoneStreamer _zoneStreamer;
        #endregion

        #region Init
        public async UniTask Init(SceneParameterMain sceneParameterMain)
        {
            _inGameContext = new();
            _objectSpawner = gameObject.AddComponent<ObjectSpawner>();
            _objectSpawner.Init(_inGameContext);

            var dialogueView = await CreateView<DialogueView>(UIDialogueView);
            if (dialogueView != null)
                dialogueView.Init();
            var screenFadeView = await CreateView<ScreenFadeView>(UIScreenFadeView);
            if (screenFadeView != null)
                screenFadeView.Init();

            var tableManager = Global.Instance.TableManager;
            var cancellationToken = this.GetCancellationTokenOnDestroy();

            //맵은 Zone 프리팹을 격자에 이어 붙여 만든다. 표가 틀리면 에러만 남기고 맞는 Zone으로 진행한다 (이야기 검사가 없는 Zone을 다시 잡는다)
            ZoneLayout.TryBuild(tableManager.MapRecord.GetAllRecord(), tableManager.ZoneRecord.GetAllRecord(), out var zoneLayout);
            var zoneRoot = new GameObject("Zones").transform;
            _zoneStreamer = new ZoneStreamer(zoneLayout, new PrefabZoneLoader(Global.Instance.AssetManager, zoneRoot),
                _inGameContext, Camera.main, cancellationToken);
            //처음부터 서 있는 캐릭터가 바닥이 생기기 전에 놓이지 않게, 시작 Zone을 다 불러온 뒤에 이야기를 시작한다
            await _zoneStreamer.LoadInitial();

            //플레이어 배치부터 적 등장까지 인게임 흐름은 퀘스트 표가 정하고, 연출은 퀘스트가 부르는 시나리오 표가 정한다
            var storyContext = new StoryContext(_inGameContext, _objectSpawner, tableManager,
                dialogueView != null ? new DialoguePlayer(_inGameContext, dialogueView) : null,
                screenFadeView,
                CreateCameraDirector(),
                zoneLayout,
                _zoneStreamer);
            _talkPlayer = new TalkPlayer(storyContext, cancellationToken);
            new StoryPlayer(storyContext).Run(cancellationToken).Forget();
        }
        #endregion

        #region LifeCycle
        private void OnDestroy()
        {
            _talkPlayer?.Dispose();
            _zoneStreamer?.Dispose();

            var assetManager = Global.Instance?.AssetManager;
            if (assetManager == null) return;

            foreach (var viewName in _loadedViews)
                assetManager.ReleaseAsset<GameObject>(LoadTarget.UI, viewName);
            _loadedViews.Clear();
        }
        #endregion

        #region Private
        /// <summary>
        /// UI 프리팹을 받아 띄운다. 없으면 null을 돌려준다.
        /// 프리팹이 없어도 배치와 전투는 확인할 수 있어야 하므로 여기서 멈추지 않고, 그 UI를 쓰는 시나리오 명령만 에러를 남긴다
        /// </summary>
        //InGame.Component 네임스페이스가 같은 이름을 가리므로 UnityEngine을 붙여 쓴다
        private async UniTask<T> CreateView<T>(string viewName) where T : UnityEngine.Component
        {
            var prefab = await Global.Instance.AssetManager.LoadAssetAsync<GameObject>(LoadTarget.UI, viewName);
            if (prefab == null)
            {
                Debug.LogError($"{viewName} is missing. Build it with the editor menu UI > Build All UI.");
                return null;
            }
            _loadedViews.Add(viewName);

            if (Instantiate(prefab).TryGetComponent(out T view)) return view;

            Debug.LogError($"{viewName} has no {typeof(T).Name}.");
            return null;
        }

        private static CameraDirector CreateCameraDirector()
        {
            var camera = Camera.main;
            if (camera != null) return new CameraDirector(camera);

            Debug.LogError("Main camera is missing. Tag the scene camera as MainCamera.");
            return null;
        }
        #endregion
    }
}
