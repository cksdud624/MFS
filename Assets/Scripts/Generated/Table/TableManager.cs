using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Generated.Table
{
	public class TableManager : MonoBehaviour
	{
		public CharacterRecord CharacterRecord {get; private set;}
		public ObjectRecord ObjectRecord {get; private set;}

		public async UniTask Init()
		{
			CharacterRecord = new ();
			await CharacterRecord.Init();
			ObjectRecord = new ();
			await ObjectRecord.Init();
		}
	}
}
