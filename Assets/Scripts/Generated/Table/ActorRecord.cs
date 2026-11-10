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
		private Dictionary<string, ActorData> datasById = new();
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
		public ActorData GetRecord(string id)
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
		public string Id {get; private set;}
		public long ObjectId {get; private set;}
		public string Type {get; private set;}
		public bool Resident {get; private set;}
		public Vector2 Position {get; private set;}
		public string Note {get; private set;}

		public ActorData(BinaryReader reader)
		{
			string[] tableDatas = reader.ReadString().Split('	');
			Id = tableDatas[0];
			ObjectId = long.TryParse(tableDatas[1], out long vLong1) ? vLong1 : 0L;
			Type = tableDatas[2];
			Resident = bool.TryParse(tableDatas[3], out bool vBool3) ? vBool3 : false;
			string[] items4 = tableDatas[4].Split(',', ';');
			if (items4.Length == 2)
			{
				float.TryParse(items4[0], out float resultX4);
				float.TryParse(items4[1], out float resultY4);
				Position = new Vector2(resultX4, resultY4);
			}
			else if (string.IsNullOrEmpty(tableDatas[4]))
			{
				Position = Vector2.zero;
			}
			else
			{
				Position = Vector2.zero;
				Debug.LogError($"Position is not Vector2 : {tableDatas[4]}");
			}
			Note = tableDatas[5];
		}
	}
}
