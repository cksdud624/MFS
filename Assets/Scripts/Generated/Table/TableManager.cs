using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Generated.Table
{
	public class TableManager : MonoBehaviour
	{
		public CharacterRecord CharacterRecord {get; private set;}
		public ObjectRecord ObjectRecord {get; private set;}
		public AttackHitBoxRecord AttackHitBoxRecord {get; private set;}
		public AttackCommandRecord AttackCommandRecord {get; private set;}
		public DialogueRecord DialogueRecord {get; private set;}
		public ActorRecord ActorRecord {get; private set;}
		public TalkRecord TalkRecord {get; private set;}
		public MapRecord MapRecord {get; private set;}
		public ZoneRecord ZoneRecord {get; private set;}
		public ScenarioRecord ScenarioRecord {get; private set;}
		public QuestRecord QuestRecord {get; private set;}

		public async UniTask Init()
		{
			CharacterRecord = new ();
			await CharacterRecord.Init();
			ObjectRecord = new ();
			await ObjectRecord.Init();
			AttackHitBoxRecord = new ();
			await AttackHitBoxRecord.Init();
			AttackCommandRecord = new ();
			await AttackCommandRecord.Init();
			DialogueRecord = new ();
			await DialogueRecord.Init();
			ActorRecord = new ();
			await ActorRecord.Init();
			TalkRecord = new ();
			await TalkRecord.Init();
			MapRecord = new ();
			await MapRecord.Init();
			ZoneRecord = new ();
			await ZoneRecord.Init();
			ScenarioRecord = new ();
			await ScenarioRecord.Init();
			QuestRecord = new ();
			await QuestRecord.Init();
		}
	}
}
