using System.IO;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Generated.Table
{
	public partial class BossRecord
	{
		private const string Key = "Assets/Generated/Table/Boss.bytes";
		private List<BossData> datas = new();
		private Dictionary<string, BossData> datasById = new();
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
					BossData data = new (reader);
					datas.Add(data);
					datasById.Add(data.Id, data);
				}
			}
			InitCustomRecord();
		}
		public BossData GetRecord(string id)
		{
			datasById.TryGetValue(id, out var record);
			return record;
		}
		public List<BossData> GetAllRecord()
		{
			return datas;
		}
	}

	public class BossData
	{
		public string Id {get; private set;}
		public int PhaseCount {get; private set;}
		public float IdleDuration {get; private set;}
		public float PhaseTransitionDuration {get; private set;}
		public string Note {get; private set;}

		public BossData(BinaryReader reader)
		{
			string[] tableDatas = reader.ReadString().Split('	');
			Id = tableDatas[0];
			PhaseCount = int.TryParse(tableDatas[1], out int vInt1) ? vInt1 : 0;
			IdleDuration = float.TryParse(tableDatas[2], out float vFloat2) ? vFloat2 : 0f;
			PhaseTransitionDuration = float.TryParse(tableDatas[3], out float vFloat3) ? vFloat3 : 0f;
			Note = tableDatas[4];
		}
	}
}
