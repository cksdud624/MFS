using System.IO;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Generated.Table
{
	public partial class ObjectRecord
	{
		private const string Key = "Assets/Generated/Table/Object.bytes";
		private List<ObjectData> datas = new();
		private Dictionary<long, ObjectData> datasById = new();
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
					ObjectData data = new (reader);
					datas.Add(data);
					datasById.Add(data.Id, data);
				}
			}
			InitCustomRecord();
		}
		public ObjectData GetRecord(long id)
		{
			datasById.TryGetValue(id, out var record);
			return record;
		}
		public List<ObjectData> GetAllRecord()
		{
			return datas;
		}
	}

	public class ObjectData
	{
		public long Id {get; private set;}
		public bool IsCharacter {get; private set;}
		public float MoveSpeed {get; private set;}
		public Vector2 ColliderSize {get; private set;}
		public Vector2 ColliderOffset {get; private set;}

		public ObjectData(BinaryReader reader)
		{
			string[] tableDatas = reader.ReadString().Split('	');
			Id = long.TryParse(tableDatas[0], out long vLong0) ? vLong0 : 0L;
			IsCharacter = bool.TryParse(tableDatas[1], out bool vBool1) ? vBool1 : false;
			MoveSpeed = float.TryParse(tableDatas[2], out float vFloat2) ? vFloat2 : 0f;
			string[] items3 = tableDatas[3].Split(',', ';');
			if (items3.Length == 2)
			{
				float.TryParse(items3[0], out float resultX3);
				float.TryParse(items3[1], out float resultY3);
				ColliderSize = new Vector2(resultX3, resultY3);
			}
			else
			{
				ColliderSize = Vector2.zero;
				Debug.LogError($"ColliderSize is not Vector2 : {tableDatas[3]}");
			}
			string[] items4 = tableDatas[4].Split(',', ';');
			if (items4.Length == 2)
			{
				float.TryParse(items4[0], out float resultX4);
				float.TryParse(items4[1], out float resultY4);
				ColliderOffset = new Vector2(resultX4, resultY4);
			}
			else
			{
				ColliderOffset = Vector2.zero;
				Debug.LogError($"ColliderOffset is not Vector2 : {tableDatas[4]}");
			}
		}
	}
}
