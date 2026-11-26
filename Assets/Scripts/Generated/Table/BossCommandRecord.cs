using System.IO;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Generated.Table
{
	public partial class BossCommandRecord
	{
		private const string Key = "Assets/Generated/Table/BossCommand.bytes";
		private List<BossCommandData> datas = new();
		private Dictionary<long, BossCommandData> datasById = new();
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
					BossCommandData data = new (reader);
					datas.Add(data);
					datasById.Add(data.Id, data);
				}
			}
			InitCustomRecord();
		}
		public BossCommandData GetRecord(long id)
		{
			datasById.TryGetValue(id, out var record);
			return record;
		}
		public List<BossCommandData> GetAllRecord()
		{
			return datas;
		}
	}

	public class BossCommandData
	{
		public long Id {get; private set;}
		public long Pattern {get; private set;}
		public string Command {get; private set;}
		public string Param1 {get; private set;}
		public string Param2 {get; private set;}
		public string Note {get; private set;}

		public BossCommandData(BinaryReader reader)
		{
			string[] tableDatas = reader.ReadString().Split('	');
			Id = long.TryParse(tableDatas[0], out long vLong0) ? vLong0 : 0L;
			Pattern = long.TryParse(tableDatas[1], out long vLong1) ? vLong1 : 0L;
			Command = tableDatas[2];
			Param1 = tableDatas[3];
			Param2 = tableDatas[4];
			Note = tableDatas[5];
		}
	}
}
