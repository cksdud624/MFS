using System.IO;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Generated.Table
{
	public partial class AttackHitBoxRecord
	{
		private const string Key = "Assets/Generated/Table/AttackHitBox.bytes";
		private List<AttackHitBoxData> datas = new();
		private Dictionary<long, AttackHitBoxData> datasById = new();
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
					AttackHitBoxData data = new (reader);
					datas.Add(data);
					datasById.Add(data.Id, data);
				}
			}
			InitCustomRecord();
		}
		public AttackHitBoxData GetRecord(long id)
		{
			datasById.TryGetValue(id, out var record);
			return record;
		}
		public List<AttackHitBoxData> GetAllRecord()
		{
			return datas;
		}
	}

	public class AttackHitBoxData
	{
		public long Id {get; private set;}
		public Vector2 HitBoxOffset {get; private set;}
		public Vector2 HitBoxSize {get; private set;}

		public AttackHitBoxData(BinaryReader reader)
		{
			string[] tableDatas = reader.ReadString().Split('	');
			Id = long.TryParse(tableDatas[0], out long vLong0) ? vLong0 : 0L;
			string[] items1 = tableDatas[1].Split(',', ';');
			if (items1.Length == 2)
			{
				float.TryParse(items1[0], out float resultX1);
				float.TryParse(items1[1], out float resultY1);
				HitBoxOffset = new Vector2(resultX1, resultY1);
			}
			else
			{
				HitBoxOffset = Vector2.zero;
				Debug.LogError($"HitBoxOffset is not Vector2 : {tableDatas[1]}");
			}
			string[] items2 = tableDatas[2].Split(',', ';');
			if (items2.Length == 2)
			{
				float.TryParse(items2[0], out float resultX2);
				float.TryParse(items2[1], out float resultY2);
				HitBoxSize = new Vector2(resultX2, resultY2);
			}
			else
			{
				HitBoxSize = Vector2.zero;
				Debug.LogError($"HitBoxSize is not Vector2 : {tableDatas[2]}");
			}
		}
	}
}
