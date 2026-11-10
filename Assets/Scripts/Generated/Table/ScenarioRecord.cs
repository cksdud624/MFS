using System.IO;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Generated.Table
{
	public partial class ScenarioRecord
	{
		private const string Key = "Assets/Generated/Table/Scenario.bytes";
		private List<ScenarioData> datas = new();
		private Dictionary<long, ScenarioData> datasById = new();
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
					ScenarioData data = new (reader);
					datas.Add(data);
					datasById.Add(data.Id, data);
				}
			}
			InitCustomRecord();
		}
		public ScenarioData GetRecord(long id)
		{
			datasById.TryGetValue(id, out var record);
			return record;
		}
		public List<ScenarioData> GetAllRecord()
		{
			return datas;
		}
	}

	public class ScenarioData
	{
		public long Id {get; private set;}
		public long Scenario {get; private set;}
		public string Command {get; private set;}
		public string Actor {get; private set;}
		public long Zone {get; private set;}
		public string Param1 {get; private set;}
		public string Param2 {get; private set;}
		public string Note {get; private set;}

		public ScenarioData(BinaryReader reader)
		{
			string[] tableDatas = reader.ReadString().Split('	');
			Id = long.TryParse(tableDatas[0], out long vLong0) ? vLong0 : 0L;
			Scenario = long.TryParse(tableDatas[1], out long vLong1) ? vLong1 : 0L;
			Command = tableDatas[2];
			Actor = tableDatas[3];
			Zone = long.TryParse(tableDatas[4], out long vLong4) ? vLong4 : 0L;
			Param1 = tableDatas[5];
			Param2 = tableDatas[6];
			Note = tableDatas[7];
		}
	}
}
