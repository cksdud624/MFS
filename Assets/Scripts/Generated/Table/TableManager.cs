using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Generated.Table
{
	public class TableManager : MonoBehaviour
	{
		public ActorRecord ActorRecord {get; private set;}
		public AttackCommandRecord AttackCommandRecord {get; private set;}
		public AttackHitBoxRecord AttackHitBoxRecord {get; private set;}
		public CharacterRecord CharacterRecord {get; private set;}
		public DialogueRecord DialogueRecord {get; private set;}
		public MapRecord MapRecord {get; private set;}
		public ObjectRecord ObjectRecord {get; private set;}
		public QuestRecord QuestRecord {get; private set;}
		public QuestInfoRecord QuestInfoRecord {get; private set;}
		public ScenarioRecord ScenarioRecord {get; private set;}
		public StatusEffectRecord StatusEffectRecord {get; private set;}
		public StatusModifierRecord StatusModifierRecord {get; private set;}
		public TalkRecord TalkRecord {get; private set;}
		public ZoneRecord ZoneRecord {get; private set;}

		public async UniTask Init()
		{
			ActorRecord = new ();
			await ActorRecord.Init();
			AttackCommandRecord = new ();
			await AttackCommandRecord.Init();
			AttackHitBoxRecord = new ();
			await AttackHitBoxRecord.Init();
			CharacterRecord = new ();
			await CharacterRecord.Init();
			DialogueRecord = new ();
			await DialogueRecord.Init();
			MapRecord = new ();
			await MapRecord.Init();
			ObjectRecord = new ();
			await ObjectRecord.Init();
			QuestRecord = new ();
			await QuestRecord.Init();
			QuestInfoRecord = new ();
			await QuestInfoRecord.Init();
			ScenarioRecord = new ();
			await ScenarioRecord.Init();
			StatusEffectRecord = new ();
			await StatusEffectRecord.Init();
			StatusModifierRecord = new ();
			await StatusModifierRecord.Init();
			TalkRecord = new ();
			await TalkRecord.Init();
			ZoneRecord = new ();
			await ZoneRecord.Init();
		}
	}
}
