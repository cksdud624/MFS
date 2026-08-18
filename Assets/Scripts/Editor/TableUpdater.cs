using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using System.Linq;

public static class TableUpdater
{
    private const string BytesDirectory = "Generated/Table";
    private const string ScriptsDirectory = "Scripts/Generated/Table";
    private const string TableDirectory = ".Table/";
    private const string TableManifestFile = "TableManifest.json";
    private const string TableManager = "TableManager";
    [MenuItem("Table/Update Table")]
    public static void UpdateTable()
    {
        string tableDirectoryPath = Path.Combine(Application.dataPath, TableDirectory);
        string tableManifestFilePath = Path.Combine(tableDirectoryPath, TableManifestFile);
        
        TableManifest tableManifest;
        if (File.Exists(tableManifestFilePath))
            tableManifest = JsonUtility.FromJson<TableManifest>(File.ReadAllText(tableManifestFilePath));
        else
            tableManifest = new TableManifest();
        
        if (!Directory.Exists(tableDirectoryPath))
            Directory.CreateDirectory(tableDirectoryPath);
        
        string[] tablePaths = Directory.GetFiles(tableDirectoryPath, "*.tsv", SearchOption.TopDirectoryOnly);
        
        TableManifest newTableManifest = new TableManifest();
        Dictionary<TableHashData, List<string>> scriptReference = new();
        foreach (var tablePath in tablePaths)
        {
            string[] lines = File.ReadAllLines(tablePath);
            string tableName = Path.GetFileNameWithoutExtension(tablePath);
            
            if (lines.Length < 2)
            {
                Debug.Log($"{tablePath} has 2 less lines");
                return;
            }

            byte[] tableDataBytes;
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                for (int i = 2; i < lines.Length; i++)
                {
                    bw.Write(lines[i]);
                }
                tableDataBytes = ms.ToArray();
            }

            string md5Hash = string.Empty;
            using (MD5 md5 = MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(tableDataBytes);
                StringBuilder sb = new StringBuilder();
                foreach (var b in hashBytes)
                    sb.Append(b.ToString("x2"));
                md5Hash = sb.ToString();
            }

            TableHashData hashData = tableManifest.tables.FirstOrDefault(data => data.fileName == tableName);
            if (hashData != null)
            {
                if (hashData.hash != md5Hash)
                {
                    CreateTable(tableName ,tableDataBytes);
                    TableHashData newHashData = new TableHashData(tableName, md5Hash);
                    newTableManifest.tables.Add(newHashData);
                    scriptReference.Add(newHashData, new List<string>{lines[0], lines[1]});
                }
                else
                {
                    newTableManifest.tables.Add(hashData);
                    scriptReference.Add(hashData, new List<string>{lines[0], lines[1]});
                }
                tableManifest.tables.Remove(hashData);
            }
            else
            {
                CreateTable(tableName, tableDataBytes);
                TableHashData newHashData = new TableHashData(tableName, md5Hash);
                newTableManifest.tables.Add(newHashData);
                scriptReference.Add(newHashData, new List<string>{lines[0], lines[1]});
            }
        }

        if (tableManifest.tables.Count > 0)
        {
            foreach (var tableName in tableManifest.tables)
            {
                string tablePath = Path.Combine(Application.dataPath, BytesDirectory, tableName.fileName) + ".bytes";
                if (File.Exists(tablePath))
                    File.Delete(tablePath);
                
                string scriptPath = Path.Combine(Application.dataPath, ScriptsDirectory, tableName.fileName) + ".cs";
                if (File.Exists(scriptPath))
                    File.Delete(scriptPath);
            }
        }
        
        string scriptDirectory = Path.Combine(Application.dataPath, ScriptsDirectory);
        string tableManager = Path.Combine(Application.dataPath, ScriptsDirectory, TableManager + ".cs");
        StringBuilder script = new StringBuilder();
        script.AppendLine("using Cysharp.Threading.Tasks;");
        script.AppendLine("using UnityEngine;");
        script.AppendLine();
        script.AppendLine("namespace Generated.Table");
        script.AppendLine("{");
        script.AppendLine($"\tpublic class {TableManager} : MonoBehaviour");
        script.AppendLine("\t{");
        foreach (var item in newTableManifest.tables)
        {
            script.AppendLine($"\t\tpublic {item.fileName}Record {item.fileName}Record {{get; private set;}}");
        }
        script.AppendLine("");
        script.AppendLine("\t\tpublic async UniTask Init()");
        script.AppendLine("\t\t{");
        foreach (var item in newTableManifest.tables)
        {
            script.AppendLine($"\t\t\t{item.fileName}Record = new ();");
            script.AppendLine($"\t\t\tawait {item.fileName}Record.Init();");
        }
        script.AppendLine("\t\t}");
        script.AppendLine("\t}");
        script.AppendLine("}");
        
        if(!Directory.Exists(scriptDirectory))
            Directory.CreateDirectory(scriptDirectory);
            
        File.WriteAllText(tableManager, script.ToString(), Encoding.UTF8);
        
        foreach (var reference in scriptReference)
        {
            string recordPath = Path.Combine(Application.dataPath, ScriptsDirectory, reference.Key.fileName + "Record.cs");
            string[] columns = reference.Value[0].Split('\t');
            string[] types = reference.Value[1].Split('\t');
            if (columns.Length != types.Length)
            {
                Debug.Log($"Column count mismatch {reference.Key.fileName}");
                return;
            }

            if (types.Length <= 0)
            {
                Debug.Log($"Column does not have ID {reference.Key.fileName}");
                return;
            }
            
            StringBuilder record = new StringBuilder();
            record.AppendLine("using System.IO;");
            record.AppendLine("using System.Collections.Generic;");
            record.AppendLine("using Cysharp.Threading.Tasks;");
            record.AppendLine("using UnityEngine;");
            record.AppendLine("using UnityEngine.AddressableAssets;");
            record.AppendLine();
            record.AppendLine("namespace Generated.Table");
            record.AppendLine("{");
            record.AppendLine($"\tpublic partial class {reference.Key.fileName}Record");
            record.AppendLine("\t{");
            record.AppendLine($"\t\tprivate const string Key = \"{"Assets/" + BytesDirectory + "/" + reference.Key.fileName + ".bytes"}\";");
            record.AppendLine($"\t\tprivate List<{reference.Key.fileName}Data> datas = new();");
            record.AppendLine($"\t\tprivate Dictionary<{types[0]}, {reference.Key.fileName}Data> datasById = new();");
            record.AppendLine("\t\tpartial void InitCustomRecord();");
            record.AppendLine("\t\tpublic async UniTask Init()");
            record.AppendLine("\t\t{");
            record.AppendLine("\t\t\tvar asset = await Addressables.LoadAssetAsync<TextAsset>(Key).ToUniTask();");
            record.AppendLine("\t\t\tif(asset == null)");
            record.AppendLine("\t\t\t\tthrow new System.OperationCanceledException($\"Load failed: {Key}\");");
            record.AppendLine("\t\t\tusing (MemoryStream ms = new MemoryStream(asset.bytes))");
            record.AppendLine("\t\t\tusing (BinaryReader reader = new BinaryReader(ms))");
            record.AppendLine("\t\t\t{");
            record.AppendLine("\t\t\t\twhile (reader.BaseStream.Position < reader.BaseStream.Length)");
            record.AppendLine("\t\t\t\t{");
            record.AppendLine($"\t\t\t\t\t{reference.Key.fileName}Data data = new (reader);");
            record.AppendLine("\t\t\t\t\tdatas.Add(data);");
            record.AppendLine($"\t\t\t\t\tdatasById.Add(data.{columns[0]}, data);");
            record.AppendLine("\t\t\t\t}");
            record.AppendLine("\t\t\t}");
            record.AppendLine("\t\t\tInitCustomRecord();");
            record.AppendLine("\t\t}");

            record.AppendLine($"\t\tpublic {reference.Key.fileName}Data GetRecord({types[0]} {columns[0].ToLower()})");
            record.AppendLine("\t\t{");
            record.AppendLine($"\t\t\tdatasById.TryGetValue({columns[0].ToLower()}, out var record);");
            record.AppendLine("\t\t\treturn record;");
            record.AppendLine("\t\t}");
            
            record.AppendLine($"\t\tpublic List<{reference.Key.fileName}Data> GetAllRecord()");
            record.AppendLine("\t\t{");
            record.AppendLine("\t\t\treturn datas;");
            record.AppendLine("\t\t}");
            
            record.AppendLine("\t}");
            record.AppendLine("");
            record.AppendLine($"\tpublic class {reference.Key.fileName}Data");
            record.AppendLine("\t{");
            for (int i = 0; i < columns.Length; i++)
                record.AppendLine($"\t\tpublic {types[i]} {columns[i]} {{get; private set;}}");
            record.AppendLine("");
            record.AppendLine($"\t\tpublic {reference.Key.fileName}Data(BinaryReader reader)");
            record.AppendLine("\t\t{");
            record.AppendLine("\t\t\tstring[] tableDatas = reader.ReadString().Split('\t');");
            
            for (int i = 0; i < columns.Length; i++)
            {
                if (types[i].Contains("List"))
                {
                    
                    string type = types[i].Replace("List<", "");
                    type = type.Replace(">", "");
                    record.AppendLine($"\t\t\t{columns[i]} = new ();");
                    record.AppendLine($"\t\t\tstring[] items{i} = tableDatas[{i}].Split(',');");
                    record.AppendLine($"\t\t\tforeach (var item in items{i})");
                    record.AppendLine("\t\t\t{");
                    record.AppendLine($"\t\t\t\t{columns[i]}.Add({GetParseType(type, "item", i)});");
                    record.AppendLine("\t\t\t}");
                    
                }
                else if (types[i].Contains("Vector3"))
                    AppendVectorParse(record, columns[i], i, 3);
                else if (types[i].Contains("Vector2"))
                    AppendVectorParse(record, columns[i], i, 2);
                else
                    record.AppendLine($"\t\t\t{columns[i]} = {GetParseType(types[i], $"tableDatas[{i}]", i)};");
            }
            record.AppendLine("\t\t}");
            record.AppendLine("\t}");
            record.AppendLine("}");
            File.WriteAllText(recordPath, record.ToString(), Encoding.UTF8);
        }

        string json = JsonUtility.ToJson(newTableManifest, true);
        string jsonPath = Path.Combine(tableDirectoryPath, TableManifestFile);
        File.WriteAllText(jsonPath, json);
        AssetDatabase.Refresh();
    }

    //Vector 계열은 "x,y" / "x;y;z" 처럼 적힌 값을 성분 수만큼 쪼개서 읽는다. 구분자는 쉼표와 세미콜론 둘 다 받는다
    private static void AppendVectorParse(StringBuilder record, string column, int index, int componentCount)
    {
        string type = $"Vector{componentCount}";
        string[] axisNames = { "X", "Y", "Z" };

        record.AppendLine($"\t\t\tstring[] items{index} = tableDatas[{index}].Split(',', ';');");
        record.AppendLine($"\t\t\tif (items{index}.Length == {componentCount})");
        record.AppendLine("\t\t\t{");
        for (int axis = 0; axis < componentCount; axis++)
            record.AppendLine($"\t\t\t\tfloat.TryParse(items{index}[{axis}], out float result{axisNames[axis]}{index});");
        string arguments = string.Join(", ", Enumerable.Range(0, componentCount).Select(axis => $"result{axisNames[axis]}{index}"));
        record.AppendLine($"\t\t\t\t{column} = new {type}({arguments});");
        record.AppendLine("\t\t\t}");
        record.AppendLine("\t\t\telse");
        record.AppendLine("\t\t\t{");
        record.AppendLine($"\t\t\t\t{column} = {type}.zero;");
        //어떤 값이 틀렸는지 알아야 하므로 결과가 아니라 원본 문자열을 남긴다
        record.AppendLine($"\t\t\t\tDebug.LogError($\"{column} is not {type} : {{tableDatas[{index}]}}\");");
        record.AppendLine("\t\t\t}");
    }

    private static string GetParseType(string type, string valueName, int index)
    {
        switch (type.ToLower())
        {
            case "long":
                return $"long.TryParse({valueName}, out long vLong{index}) ? vLong{index} : 0L";
            case "int":
                return $"int.TryParse({valueName}, out int vInt{index}) ? vInt{index} : 0";
            case "float":
                return $"float.TryParse({valueName}, out float vFloat{index}) ? vFloat{index} : 0f";
            case "bool":
                return $"bool.TryParse({valueName}, out bool vBool{index}) ? vBool{index} : false";
            case "double":
                return $"double.TryParse({valueName}, out double vDouble{index}) ? vDouble{index} : 0.0";
            case "string":
                return valueName;
        }
        return $"{type}.Parse({valueName})";
    }

    private static void CreateTable(string tableName, byte[] byteDatas)
    {
        string bytesDirectoryPath = Path.Combine(Application.dataPath, BytesDirectory);
        if(!Directory.Exists(bytesDirectoryPath))
            Directory.CreateDirectory(bytesDirectoryPath);
        string bytesPath = Path.Combine(bytesDirectoryPath, tableName + ".bytes");
        
        File.WriteAllBytes(bytesPath, byteDatas);
    }
    

    [Serializable]
    public class TableManifest
    {
        public List<TableHashData> tables = new List<TableHashData>();
    }

    [Serializable]
    public class TableHashData
    {
        public string fileName;
        public string hash;

        public TableHashData(string filename, string hash)
        {
            this.fileName = filename;
            this.hash = hash;
        }
    }
}
