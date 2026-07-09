using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using InGame.Context;
using UnityEngine;

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

        public async UniTask SpawnPlayer()
        {
        }
    }
}