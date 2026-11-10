using System.IO;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Generated.Table
{
	public partial class DialogueRecord
	{
		private const string Key = "Assets/Generated/Table/Dialogue.bytes";
		private List<DialogueData> datas = new();
		private Dictionary<long, DialogueData> datasById = new();
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
					DialogueData data = new (reader);
					datas.Add(data);
					datasById.Add(data.Id, data);
				}
			}
			InitCustomRecord();
		}
		public DialogueData GetRecord(long id)
		{
			datasById.TryGetValue(id, out var record);
			return record;
		}
		public List<DialogueData> GetAllRecord()
		{
			return datas;
		}
	}

	public class DialogueData
	{
		public long Id {get; private set;}
		public long DialogueId {get; private set;}
		public string Speaker {get; private set;}
		public string Text {get; private set;}

		public DialogueData(BinaryReader reader)
		{
			string[] tableDatas = reader.ReadString().Split('	');
			Id = long.TryParse(tableDatas[0], out long vLong0) ? vLong0 : 0L;
			DialogueId = long.TryParse(tableDatas[1], out long vLong1) ? vLong1 : 0L;
			Speaker = tableDatas[2];
			Text = tableDatas[3];
		}
	}
}
