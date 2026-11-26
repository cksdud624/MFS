using System.IO;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Generated.Table
{
	public partial class BossPatternRecord
	{
		private const string Key = "Assets/Generated/Table/BossPattern.bytes";
		private List<BossPatternData> datas = new();
		private Dictionary<long, BossPatternData> datasById = new();
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
					BossPatternData data = new (reader);
					datas.Add(data);
					datasById.Add(data.Id, data);
				}
			}
			InitCustomRecord();
		}
		public BossPatternData GetRecord(long id)
		{
			datasById.TryGetValue(id, out var record);
			return record;
		}
		public List<BossPatternData> GetAllRecord()
		{
			return datas;
		}
	}

	public class BossPatternData
	{
		public long Id {get; private set;}
		public string Boss {get; private set;}
		public string Name {get; private set;}
		public List<int> Phases {get; private set;}
		public float Weight {get; private set;}
		public int Priority {get; private set;}
		public float Cooldown {get; private set;}
		public float MinDistance {get; private set;}
		public float MaxDistance {get; private set;}
		public float RecoveryDuration {get; private set;}
		public string Note {get; private set;}

		public BossPatternData(BinaryReader reader)
		{
			string[] tableDatas = reader.ReadString().Split('	');
			Id = long.TryParse(tableDatas[0], out long vLong0) ? vLong0 : 0L;
			Boss = tableDatas[1];
			Name = tableDatas[2];
			Phases = new ();
			string[] items3 = tableDatas[3].Split(',');
			foreach (var item in items3)
			{
				Phases.Add(int.TryParse(item, out int vInt3) ? vInt3 : 0);
			}
			Weight = float.TryParse(tableDatas[4], out float vFloat4) ? vFloat4 : 0f;
			Priority = int.TryParse(tableDatas[5], out int vInt5) ? vInt5 : 0;
			Cooldown = float.TryParse(tableDatas[6], out float vFloat6) ? vFloat6 : 0f;
			MinDistance = float.TryParse(tableDatas[7], out float vFloat7) ? vFloat7 : 0f;
			MaxDistance = float.TryParse(tableDatas[8], out float vFloat8) ? vFloat8 : 0f;
			RecoveryDuration = float.TryParse(tableDatas[9], out float vFloat9) ? vFloat9 : 0f;
			Note = tableDatas[10];
		}
	}
}
