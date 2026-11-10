using System.IO;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Generated.Table
{
	public partial class MapRecord
	{
		private const string Key = "Assets/Generated/Table/Map.bytes";
		private List<MapData> datas = new();
		private Dictionary<long, MapData> datasById = new();
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
					MapData data = new (reader);
					datas.Add(data);
					datasById.Add(data.Id, data);
				}
			}
			InitCustomRecord();
		}
		public MapData GetRecord(long id)
		{
			datasById.TryGetValue(id, out var record);
			return record;
		}
		public List<MapData> GetAllRecord()
		{
			return datas;
		}
	}

	public class MapData
	{
		public long Id {get; private set;}
		public float ZoneWidth {get; private set;}
		public float ZoneHeight {get; private set;}
		public Vector2 Origin {get; private set;}
		public string Note {get; private set;}

		public MapData(BinaryReader reader)
		{
			string[] tableDatas = reader.ReadString().Split('	');
			Id = long.TryParse(tableDatas[0], out long vLong0) ? vLong0 : 0L;
			ZoneWidth = float.TryParse(tableDatas[1], out float vFloat1) ? vFloat1 : 0f;
			ZoneHeight = float.TryParse(tableDatas[2], out float vFloat2) ? vFloat2 : 0f;
			string[] items3 = tableDatas[3].Split(',', ';');
			if (items3.Length == 2)
			{
				float.TryParse(items3[0], out float resultX3);
				float.TryParse(items3[1], out float resultY3);
				Origin = new Vector2(resultX3, resultY3);
			}
			else if (string.IsNullOrEmpty(tableDatas[3]))
			{
				Origin = Vector2.zero;
			}
			else
			{
				Origin = Vector2.zero;
				Debug.LogError($"Origin is not Vector2 : {tableDatas[3]}");
			}
			Note = tableDatas[4];
		}
	}
}
