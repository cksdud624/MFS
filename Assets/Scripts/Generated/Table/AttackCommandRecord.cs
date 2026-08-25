using System.IO;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Generated.Table
{
	public partial class AttackCommandRecord
	{
		private const string Key = "Assets/Generated/Table/AttackCommand.bytes";
		private List<AttackCommandData> datas = new();
		private Dictionary<long, AttackCommandData> datasById = new();
		partial void InitCustomRecord();
		public async UniTask Init()
		{
			var asset = await Addressables.LoadAssetAsync<TextAsset>(Key).ToUniTask();
			if(asset == null)
				throw new System.OperationCanceledException($"Load failed: {Key}");
			using (MemoryStream ms = new MemoryStream(asset.bytes))
			using (BinaryReader reader = new BinaryReader(ms))
			{
				while (reader.BaseStream.Position < reader.BaseStream.Length)
				{
					AttackCommandData data = new (reader);
					datas.Add(data);
					datasById.Add(data.Id, data);
				}
			}
			InitCustomRecord();
		}
		public AttackCommandData GetRecord(long id)
		{
			datasById.TryGetValue(id, out var record);
			return record;
		}
		public List<AttackCommandData> GetAllRecord()
		{
			return datas;
		}
	}

	public class AttackCommandData
	{
		public long Id {get; private set;}
		public long ObjectId {get; private set;}
		public string Command {get; private set;}
		public int Animation {get; private set;}
		public bool IsHitRequired {get; private set;}
		public float ComboTime {get; private set;}
		public List<long> AttackHitBox {get; private set;}

		public AttackCommandData(BinaryReader reader)
		{
			string[] tableDatas = reader.ReadString().Split('	');
			Id = long.TryParse(tableDatas[0], out long vLong0) ? vLong0 : 0L;
			ObjectId = long.TryParse(tableDatas[1], out long vLong1) ? vLong1 : 0L;
			Command = tableDatas[2];
			Animation = int.TryParse(tableDatas[3], out int vInt3) ? vInt3 : 0;
			IsHitRequired = bool.TryParse(tableDatas[4], out bool vBool4) ? vBool4 : false;
			ComboTime = float.TryParse(tableDatas[5], out float vFloat5) ? vFloat5 : 0f;
			AttackHitBox = new ();
			string[] items6 = tableDatas[6].Split(',');
			foreach (var item in items6)
			{
				AttackHitBox.Add(long.TryParse(item, out long vLong6) ? vLong6 : 0L);
			}
		}
	}
}
