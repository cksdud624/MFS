using System.IO;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Generated.Table
{
	public partial class QuestRecord
	{
		private const string Key = "Assets/Generated/Table/Quest.bytes";
		private List<QuestData> datas = new();
		private Dictionary<long, QuestData> datasById = new();
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
					QuestData data = new (reader);
					datas.Add(data);
					datasById.Add(data.Id, data);
				}
			}
			InitCustomRecord();
		}
		public QuestData GetRecord(long id)
		{
			datasById.TryGetValue(id, out var record);
			return record;
		}
		public List<QuestData> GetAllRecord()
		{
			return datas;
		}
	}

	public class QuestData
	{
		public long Id {get; private set;}
		public string Quest {get; private set;}
		public string Command {get; private set;}
		public string Actor {get; private set;}
		public string Param1 {get; private set;}
		public string Param2 {get; private set;}
		public string Note {get; private set;}

		public QuestData(BinaryReader reader)
		{
			string[] tableDatas = reader.ReadString().Split('	');
			Id = long.TryParse(tableDatas[0], out long vLong0) ? vLong0 : 0L;
			Quest = tableDatas[1];
			Command = tableDatas[2];
			Actor = tableDatas[3];
			Param1 = tableDatas[4];
			Param2 = tableDatas[5];
			Note = tableDatas[6];
		}
	}
}
