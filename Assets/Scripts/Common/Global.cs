using System.Collections.Generic;
using Common.Scene;
using Common.Template;
using Common.Template.Interface;
using Cysharp.Threading.Tasks;
using Generated.Table;
using UnityEngine;

namespace Common
{
    public class Global : Singleton<Global>
    {
        #region Fields
        private readonly List<IUpdateable> _updateables = new ();
        private readonly List<IFixedUpdateable> _fixedUpdateables = new ();
        private readonly List<ILateUpdateable> _lateUpdateables = new ();

        public SceneLoader SceneLoader { get; private set; }
        public TableManager TableManager { get; private set; }
        public AssetManager AssetManager { get; private set; }
        #endregion

        #region Init
        public void Init() => Load().Forget();
        #endregion

        #region LifeCycle
        private void Update()
        {
            for (int i = _updateables.Count - 1; i >= 0; i--)
            {
                _updateables[i].OnUpdate();
            }
        }

        private void FixedUpdate()
        {
            for(int i = _fixedUpdateables.Count - 1; i >= 0; i--)
            {
                _fixedUpdateables[i].OnFixedUpdate();
            }
        }

        private void LateUpdate()
        {
            for (int i = _lateUpdateables.Count - 1; i >= 0; i--)
            {
                _lateUpdateables[i].OnLateUpdate();
            }
        }
        #endregion

        #region Public
        public void BindUpdate(IUpdateable updateable) => _updateables.Add(updateable);
        public void UnBindUpdate(IUpdateable updateable) => _updateables.Remove(updateable);
        public void BindFixedUpdate(IFixedUpdateable updateable) => _fixedUpdateables.Add(updateable);
        public void UnBindFixedUpdate(IFixedUpdateable updateable) => _fixedUpdateables.Remove(updateable);
        public void BindLateUpdate(ILateUpdateable updateable) => _lateUpdateables.Add(updateable);
        public void UnBindLateUpdate(ILateUpdateable updateable) => _lateUpdateables.Remove(updateable);
        #endregion

        #region Private
        private async UniTask Load()
        {
            AssetManager = InitGlobal<AssetManager>();
            //로딩 화면을 가장 먼저 띄운다. 표를 읽는 동안에도 화면을 덮고, 타이틀이 준비되면 걷힌다
            SceneLoader = InitGlobal<SceneLoader>();
            await SceneLoader.Init(AssetManager);
            TableManager = InitGlobal<TableManager>();
            await TableManager.Init();
#if UNITY_EDITOR
            //에디터 전용 테스트 키
            BindUpdate(new Test.EditorTestKeys());
            //보스전부터 시작하는 테스트 설정이 켜져 있으면 타이틀을 거치지 않고 바로 게임으로 들어간다.
            //어느 퀘스트부터 시작할지는 이야기 쪽(StoryPlayer)이 같은 설정을 보고 정한다
            if (Test.EditorTestSettings.StartAtBossBattle)
            {
                Debug.Log("[테스트 설정] 보스전부터 시작 : 타이틀을 건너뛴다 (메뉴 Test > Start At Boss Battle)");
                SceneLoader.LoadScene(GameDefine.SceneType.Main, new Scene.Parameter.SceneParameterMain());
                return;
            }
#endif

            //준비가 끝나면 타이틀로 간다. 게임(Main)은 타이틀에서 시작을 골랐을 때 들어간다
            SceneLoader.LoadScene(GameDefine.SceneType.Title);
        }

        private T InitGlobal<T>() where T : MonoBehaviour
        {
            GameObject dontDestroyObject = new GameObject(typeof(T).Name);
            T component = dontDestroyObject.AddComponent<T>();
            dontDestroyObject.transform.SetParent(Instance.transform);
            return component;
        }
        #endregion
    }
}
