using System.IO;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Generated.Table
{
	public partial class ZoneRecord
	{
		private const string Key = "Assets/Generated/Table/Zone.bytes";
		private List<ZoneData> datas = new();
		private Dictionary<long, ZoneData> datasById = new();
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
					ZoneData data = new (reader);
					datas.Add(data);
					datasById.Add(data.Id, data);
				}
			}
			InitCustomRecord();
		}
		public ZoneData GetRecord(long id)
		{
			datasById.TryGetValue(id, out var record);
			return record;
		}
		public List<ZoneData> GetAllRecord()
		{
			return datas;
		}
	}

	public class ZoneData
	{
		public long Id {get; private set;}
		public long Map {get; private set;}
		public int Column {get; private set;}
		public int Row {get; private set;}
		public string Note {get; private set;}

		public ZoneData(BinaryReader reader)
		{
			string[] tableDatas = reader.ReadString().Split('	');
			Id = long.TryParse(tableDatas[0], out long vLong0) ? vLong0 : 0L;
			Map = long.TryParse(tableDatas[1], out long vLong1) ? vLong1 : 0L;
			Column = int.TryParse(tableDatas[2], out int vInt2) ? vInt2 : 0;
			Row = int.TryParse(tableDatas[3], out int vInt3) ? vInt3 : 0;
			Note = tableDatas[4];
		}
	}
}
