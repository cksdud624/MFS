using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Generated.Table;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Object
{
    public class ObjectSpawner : MonoBehaviour
    {
        #region Fields
        private InGameContext _inGameContext;

        private List<ObjectBase> _objects = new ();
        //싸움에 끼는 캐릭터(플레이어, 적)
        private List<CharacterBase> _characters = new ();
        //NPC. 나중에 싸움에 끼게 바꿀 수 있도록 CharacterBase로 만들지만, 중립인 동안은 싸움에 끼는 캐릭터와 섞지 않는다
        private List<ObjectBase> _npcs = new ();

        //구간 안에서 번호가 겹치지 않도록 생성한 개수를 센다
        private int _objectOrder;
        private int _characterOrder;
        #endregion

        #region Init
        public void Init(InGameContext inGameContext)
        {
            _inGameContext = inGameContext;
        }
        #endregion

        #region Public
        /// <summary>플레이어를 생성한다. 적과 마찬가지로 컨트롤러(입력)까지 바로 붙인다</summary>
        public async UniTask SpawnPlayer(ObjectData objectData, Vector2? position = null)
        {
            if (_inGameContext.Player != null || objectData == null)
            {
                Debug.Log("Object Spawner Error");
                return;
            }

            ObjectBase player = await Spawn(objectData, position, Team.Player, true);
            if (player == null) return;

            _inGameContext.SetPlayer(player);
            player.AttachController();
        }

        /// <summary>적을 생성한다. 컨트롤러(AI)까지 바로 붙인다</summary>
        public async UniTask<ObjectBase> SpawnEnemy(ObjectData objectData, Vector2? position = null)
        {
            ObjectBase spawned = await Spawn(objectData, position, Team.Enemy, false);
            spawned?.AttachController();
            return spawned;
        }

        /// <summary>NPC를 생성한다. 컨트롤러를 붙이지 않으므로 제자리에 서 있기만 하고, 중립이라 싸움에 끼지 않는다</summary>
        public UniTask<ObjectBase> SpawnNpc(ObjectData objectData, Vector2? position = null)
            => Spawn(objectData, position, Team.Neutral, false);

        /// <summary>
        /// 생성한 오브젝트를 없앤다. 목록에서도 빼서 나중에 대상을 찾을 때 걸리지 않게 한다.
        /// 플레이어는 InGameContext가 들고 있어서 여기서 없애면 참조가 끊기므로 없애지 않는다
        /// </summary>
        public void Despawn(ObjectBase target)
        {
            if (target == null) return;
            if (target.IsPlayer)
            {
                Debug.LogError("Player cannot be despawned.");
                return;
            }

            _objects.Remove(target);
            _npcs.Remove(target);
            if (target is CharacterBase character)
                _characters.Remove(character);
            Destroy(target.gameObject);
        }
        #endregion

        #region Private
        //진영은 여기서 정해서 넘긴다. 같은 데이터(Id)도 어떻게 생성하느냐에 따라 플레이어, 적, NPC가 된다
        private async UniTask<ObjectBase> Spawn(ObjectData objectData, Vector2? position, Team team, bool isPlayer)
        {
            if (objectData == null)
            {
                Debug.Log("Object Spawner Error");
                return null;
            }

            var spawnedObject = new GameObject(objectData.Id.ToString());
            ObjectBase spawned = objectData.IsCharacter
                ? spawnedObject.AddComponent<CharacterBase>()
                : spawnedObject.AddComponent<ObjectBase>();

            spawned.transform.position = position ?? Vector2.zero;
            await spawned.Init(_inGameContext, objectData, team, isPlayer);
            spawned.SetSortingOrder(GetSortingOrder(spawned));

            _objects.Add(spawned);
            if (team == Team.Neutral)
                _npcs.Add(spawned);
            else if (spawned is CharacterBase character)
                _characters.Add(character);

            return spawned;
        }

        /// <summary>플레이어는 항상 맨 앞, 나머지는 종류별 구간 안에서 생성 순서대로 쌓는다</summary>
        private int GetSortingOrder(ObjectBase spawned)
        {
            if (spawned.IsPlayer)
                return SortingOrderPlayer;

            if (spawned is CharacterBase)
                return SortingOrderCharacter + _characterOrder++ % SortingOrderRange;

            return SortingOrderObject + _objectOrder++ % SortingOrderRange;
        }
        #endregion
    }
}
