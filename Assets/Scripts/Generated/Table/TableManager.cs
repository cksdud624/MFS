using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Generated.Table
{
	public class TableManager : MonoBehaviour
	{
		public AttackCommandRecord AttackCommandRecord {get; private set;}
		public AttackHitBoxRecord AttackHitBoxRecord {get; private set;}
		public CharacterRecord CharacterRecord {get; private set;}
		public ObjectRecord ObjectRecord {get; private set;}

		public async UniTask Init()
		{
			AttackCommandRecord = new ();
			await AttackCommandRecord.Init();
			AttackHitBoxRecord = new ();
			await AttackHitBoxRecord.Init();
			CharacterRecord = new ();
			await CharacterRecord.Init();
			ObjectRecord = new ();
			await ObjectRecord.Init();
		}
	}
}
