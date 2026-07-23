using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Generated.Table
{
	public class TableManager : MonoBehaviour
	{
		public ObjectRecord ObjectRecord {get; private set;}
		public CharacterRecord CharacterRecord {get; private set;}

		public async UniTask Init()
		{
			ObjectRecord = new ();
			await ObjectRecord.Init();
			CharacterRecord = new ();
			await CharacterRecord.Init();
		}
	}
}
