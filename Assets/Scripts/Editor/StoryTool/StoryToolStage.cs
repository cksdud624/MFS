using System;
using System.Collections.Generic;
using InGame.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Common.AssetKeys;
using static Common.GameDefine;

namespace StoryTool
{
    /// <summary>
    /// 시나리오 툴의 배치 화면. 프리팹 편집 화면처럼 Scene 뷰를 저장되지 않는 임시 씬으로 바꿔서, 거기에 Zone 프리팹을 깔고 대상을 세워 보여준다.
    /// 열려 있던 씬은 건드리지 않고, 닫으면 깔아 둔 것이 모두 사라진다. 여기 놓인 오브젝트는 보여주기용이고 고치는 대상은 표다.
    /// 무엇을 깔고 누구를 세울지는 툴 창이 정해서 넘긴다
    /// </summary>
    public class StoryToolStage : PreviewSceneStage
    {
        #region Types
        /// <summary>배치 화면에 세울 대상 하나</summary>
        public readonly struct ActorPreview
        {
            //표의 Actor 칸 그대로 (Player 또는 Actor 번호)
            public string Key { get; }
            public long ObjectId { get; }
            public Color Color { get; }
            public Vector2 World { get; }
            public int SortingOrder { get; }

            public ActorPreview(string key, long objectId, Color color, Vector2 world, int sortingOrder)
            {
                Key = key;
                ObjectId = objectId;
                Color = color;
                World = world;
                SortingOrder = sortingOrder;
            }
        }
        #endregion

        #region Fields
        private const string HeaderText = "시나리오 배치";

        //배치 화면이 닫히면 알린다. Scene 뷰 위의 길(Scenes)을 눌러 나가도 닫히므로 툴 창이 이것으로 안다
        public static event Action<StoryToolStage> OnClosed;

        //Zone 번호 → 깔아 둔 Zone. 프리팹이 없는 Zone은 null로 넣어 두고 다시 찾지 않는다
        private readonly Dictionary<long, GameObject> _zones = new();
        private readonly Dictionary<string, SpriteRenderer> _actors = new();
        //Object 번호 → 대상을 세울 때 쓰는 그림 (Idle 클립의 첫 그림). 없으면 null
        private readonly Dictionary<long, Sprite> _sprites = new();
        //지울 것을 모을 때 다시 쓰는 목록
        private readonly List<long> _zoneBuffer = new();
        private readonly List<string> _actorBuffer = new();
        //배치 화면을 띄운 Scene 뷰 → 띄우기 전의 조명 설정. 닫을 때 되돌린다
        private readonly Dictionary<SceneView, bool> _previousLighting = new();

        public bool IsCurrent => StageUtility.GetCurrentStage() == this;
        #endregion

        #region LifeCycle
        /// <summary>
        /// 배치 화면은 조명 없이 그림 그대로 본다. 조명을 켜면 전역 조명이 없는 이 임시 씬에서는 Zone과 대상이 검게 나온다
        /// (전역 조명은 Main 씬에 있다). 자리를 잡는 화면이라 게임과 같은 밝기보다 잘 보이는 쪽이 낫다.
        /// 위치는 x, y로만 적으므로 2D로 본다
        /// </summary>
        protected override void OnFirstTimeOpenStageInSceneView(SceneView sceneView)
        {
            _previousLighting[sceneView] = sceneView.sceneLighting;
            sceneView.sceneLighting = false;
            sceneView.in2DMode = true;
        }

        protected override void OnCloseStage()
        {
            foreach (var pair in _previousLighting)
            {
                if (pair.Key != null)
                    pair.Key.sceneLighting = pair.Value;
            }
            _previousLighting.Clear();

            //계층 창에서 배치 화면의 오브젝트를 골라 뒀으면 선택을 푼다. 사라진 오브젝트를 인스펙터가 그리려다 에러를 낸다
            foreach (var selected in Selection.gameObjects)
            {
                if (selected.scene != scene) continue;
                Selection.activeObject = null;
                break;
            }

            //임시 씬이 닫히면서 오브젝트도 같이 사라진다
            _zones.Clear();
            _actors.Clear();
            base.OnCloseStage();
            OnClosed?.Invoke(this);
        }

        protected override GUIContent CreateHeaderContent() => new(HeaderText);
        #endregion

        #region Public
        /// <summary>배치 화면을 열고 Scene 뷰를 그쪽으로 바꾼다</summary>
        public static StoryToolStage Open()
        {
            var stage = CreateInstance<StoryToolStage>();
            StageUtility.GoToStage(stage, true);
            return stage;
        }

        /// <summary>깔려 있는 Zone을 zones와 같게 맞춘다. 이미 깔린 Zone은 그대로 두고, 빠진 것은 지우고, 새 것만 깐다</summary>
        public void ShowZones(IReadOnlyCollection<ZoneLayout.ZoneEntry> zones)
        {
            var wanted = new HashSet<long>();
            bool added = false;
            foreach (var zone in zones)
            {
                wanted.Add(zone.Id);
                if (_zones.ContainsKey(zone.Id)) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GetAddressableKey(LoadTarget.Zone, zone.Id.ToString()));
                GameObject instance = null;
                if (prefab != null)
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    //Zone 프리팹은 루트가 Zone의 왼쪽 아래다
                    instance.transform.position = zone.Rect.position;
                }
                _zones[zone.Id] = instance;
                added = true;
            }
            //옮겨 놓은 Zone의 콜라이더는 물리에 알려야 그 자리에서 잡힌다. 알리지 않으면 바닥 찾기가 프리팹의 원래 자리를 본다
            if (added)
                Physics2D.SyncTransforms();

            _zoneBuffer.Clear();
            foreach (var id in _zones.Keys)
            {
                if (!wanted.Contains(id))
                    _zoneBuffer.Add(id);
            }
            foreach (var id in _zoneBuffer)
            {
                if (_zones[id] != null)
                    DestroyImmediate(_zones[id]);
                _zones.Remove(id);
            }
        }

        /// <summary>세워 둔 대상을 actors와 같게 맞춘다</summary>
        public void ShowActors(IReadOnlyList<ActorPreview> actors)
        {
            var wanted = new HashSet<string>();
            foreach (var actor in actors)
            {
                wanted.Add(actor.Key);
                if (!_actors.TryGetValue(actor.Key, out var renderer))
                {
                    renderer = CreateObject(actor.Key).AddComponent<SpriteRenderer>();
                    _actors[actor.Key] = renderer;
                }

                renderer.sprite = GetSprite(actor.ObjectId);
                renderer.color = actor.Color;
                renderer.sortingOrder = actor.SortingOrder;
                renderer.transform.position = actor.World;
            }

            _actorBuffer.Clear();
            foreach (var key in _actors.Keys)
            {
                if (!wanted.Contains(key))
                    _actorBuffer.Add(key);
            }
            foreach (var key in _actorBuffer)
            {
                DestroyImmediate(_actors[key].gameObject);
                _actors.Remove(key);
            }
        }

        /// <summary>대상에 그림이 있는지. 없으면 Scene 뷰에 몸통 크기의 틀이라도 그려야 어디 서 있는지 보인다</summary>
        public bool HasSprite(long objectId) => GetSprite(objectId) != null;

        /// <summary>
        /// 그 자리 가까이(위아래 range 안)에 있는 바닥의 높이를 찾는다. 대상을 끌어 놓을 때 발밑을 바닥에 붙이는 데 쓴다.
        /// 깔아 둔 Zone의 콜라이더에 위에서 아래로 선을 쏴서 찾는다. 임시 씬은 물리 공간이 따로 있어서 그 공간에 쏜다
        /// </summary>
        public bool TryFindGround(Vector2 world, float range, out float groundY)
        {
            groundY = world.y;
            var origin = new Vector2(world.x, world.y + range);
            var hit = scene.GetPhysicsScene2D().Raycast(origin, Vector2.down, range * 2f);
            //콜라이더 안에서 쏘기 시작했으면 그 표면은 바닥이 아니다 (벽이나 두꺼운 지형의 속이다)
            if (hit.collider == null || hit.collider.isTrigger || hit.distance <= 0f) return false;

            //물리는 콜라이더끼리 이만큼 띄워서 세운다. 표면에 딱 맞추면 바닥에 살짝 박힌 채로 등장한다
            groundY = hit.point.y + Physics2D.defaultContactOffset;
            return true;
        }
        #endregion

        #region Private
        //만들자마자 임시 씬으로 옮긴다. 열려 있는 씬에 남으면 그 씬이 바뀐 것으로 표시된다
        private GameObject CreateObject(string objectName)
        {
            var created = new GameObject(objectName);
            SceneManager.MoveGameObjectToScene(created, scene);
            return created;
        }

        private Sprite GetSprite(long objectId)
        {
            if (_sprites.TryGetValue(objectId, out var sprite)) return sprite;

            sprite = LoadIdleSprite(objectId);
            _sprites[objectId] = sprite;
            return sprite;
        }

        //게임은 애니메이션으로만 그림을 바꾸므로 캐릭터의 그림은 클립 안에 있다. 서 있는 클립의 첫 그림을 꺼내 쓴다
        private static Sprite LoadIdleSprite(long objectId)
        {
            string path = GetAddressableKey(LoadTarget.Animation, $"{objectId}/{AnimationType.Idle}");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) return null;

            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (binding.type != typeof(SpriteRenderer)) continue;

                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                if (keys.Length > 0 && keys[0].value is Sprite sprite) return sprite;
            }
            return null;
        }
        #endregion
    }
}
