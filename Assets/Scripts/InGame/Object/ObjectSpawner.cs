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
        private InGameContext _inGameContext;

        private List<ObjectBase> _objects = new ();
        private List<CharacterBase> _characters = new ();

        //구간 안에서 번호가 겹치지 않도록 생성한 개수를 센다
        private int _objectOrder;
        private int _characterOrder;

        public async UniTask Init(InGameContext inGameContext)
        {
            _inGameContext = inGameContext;
            await UniTask.CompletedTask;
        }

        public async UniTask SpawnPlayer(ObjectData objectData, Vector2? position = null)
        {
            if (_inGameContext.Player != null || objectData == null)
            {
                Debug.Log("Object Spawner Error");
                return;
            }

            ObjectBase player = await Spawn(objectData, position, true);
            if (player == null) return;

            _inGameContext.SetPlayer(player);
        }

        /// <summary>플레이어가 아닌 오브젝트를 생성한다. 컨트롤러(AI)까지 바로 붙인다</summary>
        public async UniTask<ObjectBase> SpawnObject(ObjectData objectData, Vector2? position = null)
        {
            ObjectBase spawned = await Spawn(objectData, position, false);
            spawned?.AttachController();
            return spawned;
        }

        private async UniTask<ObjectBase> Spawn(ObjectData objectData, Vector2? position, bool isPlayer)
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
            await spawned.Init(_inGameContext, objectData, isPlayer);
            spawned.SetSortingOrder(GetSortingOrder(spawned));

            _objects.Add(spawned);
            if (spawned is CharacterBase character)
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
    }
}
