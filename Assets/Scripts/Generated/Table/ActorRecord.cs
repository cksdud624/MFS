using System.IO;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Generated.Table
{
	public partial class ActorRecord
	{
		private const string Key = "Assets/Generated/Table/Actor.bytes";
		private List<ActorData> datas = new();
		private Dictionary<long, ActorData> datasById = new();
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
					ActorData data = new (reader);
					datas.Add(data);
					datasById.Add(data.Id, data);
				}
			}
			InitCustomRecord();
		}
		public ActorData GetRecord(long id)
		{
			datasById.TryGetValue(id, out var record);
			return record;
		}
		public List<ActorData> GetAllRecord()
		{
			return datas;
		}
	}

	public class ActorData
	{
		public long Id {get; private set;}
		public long ObjectId {get; private set;}
		public string Type {get; private set;}
		public string Name {get; private set;}
		public bool Resident {get; private set;}
		public long Zone {get; private set;}
		public Vector2 Position {get; private set;}
		public string Note {get; private set;}

		public ActorData(BinaryReader reader)
		{
			string[] tableDatas = reader.ReadString().Split('	');
			Id = long.TryParse(tableDatas[0], out long vLong0) ? vLong0 : 0L;
			ObjectId = long.TryParse(tableDatas[1], out long vLong1) ? vLong1 : 0L;
			Type = tableDatas[2];
			Name = tableDatas[3];
			Resident = bool.TryParse(tableDatas[4], out bool vBool4) ? vBool4 : false;
			Zone = long.TryParse(tableDatas[5], out long vLong5) ? vLong5 : 0L;
			string[] items6 = tableDatas[6].Split(',', ';');
			if (items6.Length == 2)
			{
				float.TryParse(items6[0], out float resultX6);
				float.TryParse(items6[1], out float resultY6);
				Position = new Vector2(resultX6, resultY6);
			}
			else if (string.IsNullOrEmpty(tableDatas[6]))
			{
				Position = Vector2.zero;
			}
			else
			{
				Position = Vector2.zero;
				Debug.LogError($"Position is not Vector2 : {tableDatas[6]}");
			}
			Note = tableDatas[7];
		}
	}
}
