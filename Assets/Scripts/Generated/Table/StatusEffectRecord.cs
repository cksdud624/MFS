using System.IO;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Generated.Table
{
	public partial class StatusEffectRecord
	{
		private const string Key = "Assets/Generated/Table/StatusEffect.bytes";
		private List<StatusEffectData> datas = new();
		private Dictionary<long, StatusEffectData> datasById = new();
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
					StatusEffectData data = new (reader);
					datas.Add(data);
					datasById.Add(data.Id, data);
				}
			}
			InitCustomRecord();
		}
		public StatusEffectData GetRecord(long id)
		{
			datasById.TryGetValue(id, out var record);
			return record;
		}
		public List<StatusEffectData> GetAllRecord()
		{
			return datas;
		}
	}

	public class StatusEffectData
	{
		public long Id {get; private set;}
		public float Duration {get; private set;}
		public int MaxStack {get; private set;}
		public string Note {get; private set;}

		public StatusEffectData(BinaryReader reader)
		{
			string[] tableDatas = reader.ReadString().Split('	');
			Id = long.TryParse(tableDatas[0], out long vLong0) ? vLong0 : 0L;
			Duration = float.TryParse(tableDatas[1], out float vFloat1) ? vFloat1 : 0f;
			MaxStack = int.TryParse(tableDatas[2], out int vInt2) ? vInt2 : 0;
			Note = tableDatas[3];
		}
	}
}
