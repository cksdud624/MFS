using System.IO;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Generated.Table
{
	public partial class StatusModifierRecord
	{
		private const string Key = "Assets/Generated/Table/StatusModifier.bytes";
		private List<StatusModifierData> datas = new();
		private Dictionary<long, StatusModifierData> datasById = new();
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
					StatusModifierData data = new (reader);
					datas.Add(data);
					datasById.Add(data.Id, data);
				}
			}
			InitCustomRecord();
		}
		public StatusModifierData GetRecord(long id)
		{
			datasById.TryGetValue(id, out var record);
			return record;
		}
		public List<StatusModifierData> GetAllRecord()
		{
			return datas;
		}
	}

	public class StatusModifierData
	{
		public long Id {get; private set;}
		public long StatusEffect {get; private set;}
		public string Kind {get; private set;}
		public string Target {get; private set;}
		public string Operation {get; private set;}
		public float Value {get; private set;}
		public string Note {get; private set;}

		public StatusModifierData(BinaryReader reader)
		{
			string[] tableDatas = reader.ReadString().Split('	');
			Id = long.TryParse(tableDatas[0], out long vLong0) ? vLong0 : 0L;
			StatusEffect = long.TryParse(tableDatas[1], out long vLong1) ? vLong1 : 0L;
			Kind = tableDatas[2];
			Target = tableDatas[3];
			Operation = tableDatas[4];
			Value = float.TryParse(tableDatas[5], out float vFloat5) ? vFloat5 : 0f;
			Note = tableDatas[6];
		}
	}
}
