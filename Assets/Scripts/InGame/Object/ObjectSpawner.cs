using System.Collections.Generic;
using Common;
using Cysharp.Threading.Tasks;
using Generated.Table;
using InGame.Context;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace InGame.Object
{
    public class ObjectSpawner : MonoBehaviour
    {
        private InGameContext _inGameContext;

        private List<ObjectBase> _objects = new ();
        private List<CharacterBase> _characters = new ();
        
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

            Vector2 spawnPosition = position ?? Vector2.zero;

            if (objectData.IsCharacter)
            {
                CharacterBase player = new GameObject(objectData.Id.ToString()).AddComponent<CharacterBase>();
                player.transform.position = spawnPosition;
                await player.Init(_inGameContext, objectData, true);
                _objects.Add(player);
                _characters.Add(player);
                _inGameContext.SetPlayer(player);
            }
            else
            {
                ObjectBase player = new GameObject(objectData.Id.ToString()).AddComponent<ObjectBase>();
                player.transform.position = spawnPosition;
                await player.Init(_inGameContext, objectData, true);
                _objects.Add(player);
                _inGameContext.SetPlayer(player);
            }
        }
    }
}