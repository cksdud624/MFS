using System.IO;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Generated.Table
{
	public partial class CharacterRecord
	{
		private const string Key = "Assets/Generated/Table/Character.bytes";
		private List<CharacterData> datas = new();
		private Dictionary<long, CharacterData> datasById = new();
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
					CharacterData data = new (reader);
					datas.Add(data);
					datasById.Add(data.Id, data);
				}
			}
			InitCustomRecord();
		}
		public CharacterData GetRecord(long id)
		{
			datasById.TryGetValue(id, out var record);
			return record;
		}
		public List<CharacterData> GetAllRecord()
		{
			return datas;
		}
	}

	public class CharacterData
	{
		public long Id {get; private set;}
		public int Hp {get; private set;}
		public int MachineAttack {get; private set;}
		public int MachineDefense {get; private set;}
		public int MagicAttack {get; private set;}
		public int MagicDefense {get; private set;}
		public List<long> StatusEffects {get; private set;}

		public CharacterData(BinaryReader reader)
		{
			string[] tableDatas = reader.ReadString().Split('	');
			Id = long.TryParse(tableDatas[0], out long vLong0) ? vLong0 : 0L;
			Hp = int.TryParse(tableDatas[1], out int vInt1) ? vInt1 : 0;
			MachineAttack = int.TryParse(tableDatas[2], out int vInt2) ? vInt2 : 0;
			MachineDefense = int.TryParse(tableDatas[3], out int vInt3) ? vInt3 : 0;
			MagicAttack = int.TryParse(tableDatas[4], out int vInt4) ? vInt4 : 0;
			MagicDefense = int.TryParse(tableDatas[5], out int vInt5) ? vInt5 : 0;
			StatusEffects = new ();
			string[] items6 = tableDatas[6].Split(',');
			foreach (var item in items6)
			{
				StatusEffects.Add(long.TryParse(item, out long vLong6) ? vLong6 : 0L);
			}
		}
	}
}
