using System.IO;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Generated.Table
{
	public partial class TalkRecord
	{
		private const string Key = "Assets/Generated/Table/Talk.bytes";
		private List<TalkData> datas = new();
		private Dictionary<long, TalkData> datasById = new();
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
					TalkData data = new (reader);
					datas.Add(data);
					datasById.Add(data.Id, data);
				}
			}
			InitCustomRecord();
		}
		public TalkData GetRecord(long id)
		{
			datasById.TryGetValue(id, out var record);
			return record;
		}
		public List<TalkData> GetAllRecord()
		{
			return datas;
		}
	}

	public class TalkData
	{
		public long Id {get; private set;}
		public string Actor {get; private set;}
		public string Quest {get; private set;}
		public long Dialogue {get; private set;}
		public string Note {get; private set;}

		public TalkData(BinaryReader reader)
		{
			string[] tableDatas = reader.ReadString().Split('	');
			Id = long.TryParse(tableDatas[0], out long vLong0) ? vLong0 : 0L;
			Actor = tableDatas[1];
			Quest = tableDatas[2];
			Dialogue = long.TryParse(tableDatas[3], out long vLong3) ? vLong3 : 0L;
			Note = tableDatas[4];
		}
	}
}
