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
			Resident = bool.TryParse(tableDatas[3], out bool vBool3) ? vBool3 : false;
			Zone = long.TryParse(tableDatas[4], out long vLong4) ? vLong4 : 0L;
			string[] items5 = tableDatas[5].Split(',', ';');
			if (items5.Length == 2)
			{
				float.TryParse(items5[0], out float resultX5);
				float.TryParse(items5[1], out float resultY5);
				Position = new Vector2(resultX5, resultY5);
			}
			else if (string.IsNullOrEmpty(tableDatas[5]))
			{
				Position = Vector2.zero;
			}
			else
			{
				Position = Vector2.zero;
				Debug.LogError($"Position is not Vector2 : {tableDatas[5]}");
			}
			Note = tableDatas[6];
		}
	}
}
