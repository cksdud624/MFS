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
    #region Fields
    private const string BytesDirectory = "Generated/Table";
    private const string ScriptsDirectory = "Scripts/Generated/Table";
    private const string TableDirectory = ".Table/";
    private const string TableManifestFile = "TableManifest.json";
    private const string TableManager = "TableManager";
    //챕터 폴더 이름. 뒤에 챕터 번호를 붙인다 (Chapter1). 이야기 표처럼 챕터마다 길어지는 표를 나눠 둔다
    public const string ChapterDirectoryPrefix = "Chapter";
    private const string TableExtension = "*.tsv";
    //첫 칸(Id)은 생성된 Record가 키로 쓴다. 키로 쓸 수 있는 타입. Vector2나 List는 값으로 비교할 수 없어서 키가 되지 못한다
    private static readonly string[] KeyTypes = { "long", "int", "string" };

    private static string TableDirectoryPath => Path.Combine(Application.dataPath, TableDirectory);
    #endregion

    #region Public
    [MenuItem("Table/Update Table")]
    public static void UpdateTable() => Run();

    /// <summary>표를 갱신하고 끝까지 했는지 돌려준다. 틀린 표가 있어서 멈췄으면 false다. 메뉴와 시나리오 툴이 같이 쓴다</summary>
    public static bool Run()
    {
        string tableDirectoryPath = TableDirectoryPath;
        string tableManifestFilePath = Path.Combine(tableDirectoryPath, TableManifestFile);
        
        TableManifest tableManifest;
        if (File.Exists(tableManifestFilePath))
            tableManifest = JsonUtility.FromJson<TableManifest>(File.ReadAllText(tableManifestFilePath));
        else
            tableManifest = new TableManifest();
        
        if (!Directory.Exists(tableDirectoryPath))
            Directory.CreateDirectory(tableDirectoryPath);
        
        //하나라도 틀린 표가 있으면 아무것도 갱신하지 않는다. 일부만 갱신하면 표끼리 번호가 어긋난 채로 남는다
        bool valid = TryCollectTables(out var tableFiles);
        var tables = new SortedDictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var pair in tableFiles)
        {
            if (TryMergeTable(pair.Value, out var mergedLines))
                tables[pair.Key] = mergedLines;
            else
                valid = false;
        }
        if (!valid)
        {
            Debug.LogError("Update Table stopped. Fix the errors above. Nothing is updated.");
            return false;
        }

        TableManifest newTableManifest = new TableManifest();
        Dictionary<TableHashData, List<string>> scriptReference = new();
        foreach (var table in tables)
        {
            string tableName = table.Key;
            string[] lines = table.Value;

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
                
                //생성되는 파일 이름은 XxxRecord.cs다. .meta도 같이 지워야 Unity가 없는 파일의 .meta를 남겼다고 경고하지 않는다
                string scriptPath = Path.Combine(Application.dataPath, ScriptsDirectory, tableName.fileName) + "Record.cs";
                if (File.Exists(scriptPath))
                    File.Delete(scriptPath);
                if (File.Exists(scriptPath + ".meta"))
                    File.Delete(scriptPath + ".meta");
                if (File.Exists(tablePath + ".meta"))
                    File.Delete(tablePath + ".meta");
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
        return true;
    }

    /// <summary>
    /// 표 이름 → 그 표를 이루는 tsv 파일들. 맨 위 폴더의 파일은 혼자서 표 하나이고,
    /// 챕터 폴더(Chapter1, Chapter2 ...)에 같은 이름으로 있는 파일들은 챕터 순으로 이어 붙여 표 하나가 된다.
    /// 폴더 이름이 틀렸거나 같은 표가 맨 위와 챕터 폴더에 같이 있으면 에러를 남기고 false를 돌려준다
    /// </summary>
    public static bool TryCollectTables(out SortedDictionary<string, List<string>> tables)
    {
        tables = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        string root = TableDirectoryPath;
        if (!Directory.Exists(root)) return true;

        foreach (var path in Directory.GetFiles(root, TableExtension, SearchOption.TopDirectoryOnly))
            tables[Path.GetFileNameWithoutExtension(path)] = new List<string> { path };
        var topLevelTables = new HashSet<string>(tables.Keys);

        bool valid = TryGetChapterDirectories(root, out var chapterDirectories);
        foreach (var directory in chapterDirectories)
        {
            foreach (var path in Directory.GetFiles(directory, TableExtension, SearchOption.TopDirectoryOnly))
            {
                string tableName = Path.GetFileNameWithoutExtension(path);
                //맨 위에도 있으면 어느 쪽이 그 표인지 정할 수 없다. 이어 붙이면 같은 줄이 두 번 들어갈 수 있다
                if (topLevelTables.Contains(tableName))
                {
                    valid = LogTableError(ToLabel(path), $"{tableName}.tsv is also at the top folder. Keep a table either at the top or in chapter folders.");
                    continue;
                }

                if (!tables.TryGetValue(tableName, out var files))
                {
                    files = new List<string>();
                    tables[tableName] = files;
                }
                files.Add(path);
            }
        }
        return valid;
    }
    #endregion

    #region Private
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
        //빈칸은 그 줄에서 쓰지 않는 값이므로 에러 없이 0으로 둔다. 숫자 컬럼이 빈칸을 0으로 읽는 것과 맞춘다
        record.AppendLine($"\t\t\telse if (string.IsNullOrEmpty(tableDatas[{index}]))");
        record.AppendLine("\t\t\t{");
        record.AppendLine($"\t\t\t\t{column} = {type}.zero;");
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

    /// <summary>
    /// 챕터 폴더를 챕터 번호 순으로 모은다. Chapter10이 Chapter2보다 앞에 오지 않게 이름이 아니라 번호로 정렬한다.
    /// 챕터 폴더가 아닌 폴더는 표가 들어 있어도 읽히지 않으므로 에러로 알린다
    /// </summary>
    private static bool TryGetChapterDirectories(string root, out List<string> directories)
    {
        bool valid = true;
        var chapters = new SortedDictionary<int, string>();
        foreach (var directory in Directory.GetDirectories(root))
        {
            string name = Path.GetFileName(directory);
            string number = name.StartsWith(ChapterDirectoryPrefix, StringComparison.Ordinal) ? name.Substring(ChapterDirectoryPrefix.Length) : string.Empty;
            if (number.Length == 0 || !number.All(char.IsDigit) || !int.TryParse(number, out int chapter) || chapter <= 0)
            {
                valid = LogTableError(ToLabel(directory), $"folder name must be {ChapterDirectoryPrefix} + chapter number such as {ChapterDirectoryPrefix}1.");
                continue;
            }
            //Chapter1과 Chapter01은 같은 챕터다. 둘 다 두면 이어 붙이는 순서를 정할 수 없다
            if (chapters.TryGetValue(chapter, out var other))
            {
                valid = LogTableError(ToLabel(directory), $"is the same chapter as {ToLabel(other)}.");
                continue;
            }
            chapters[chapter] = directory;
        }
        directories = chapters.Values.ToList();
        return valid;
    }

    /// <summary>
    /// 표를 이루는 파일들의 모양을 확인하고 한 표로 이어 붙인다. 생성된 코드는 칸 수가 맞고 Id가 겹치지 않는다고 보고 읽으므로,
    /// 여기서 놓치면 게임에서 표를 불러올 때 칸을 벗어나거나 키가 겹쳐서 멈춘다.
    /// 머리 두 줄(컬럼 이름, 타입)은 파일마다 같아야 하고, Id는 파일을 넘어서도 겹치면 안 된다. 게임은 이어 붙인 표 하나를 읽는다
    /// </summary>
    private static bool TryMergeTable(List<string> files, out string[] lines)
    {
        lines = null;
        string firstLabel = ToLabel(files[0]);
        string[] firstLines = File.ReadAllLines(files[0]);
        if (!ValidateHeader(firstLabel, firstLines)) return false;

        string[] columns = firstLines[0].Split('\t');
        string keyType = firstLines[1].Split('\t')[0];
        var merged = new List<string> { firstLines[0], firstLines[1] };
        //Id → 처음 나온 파일과 줄. 숫자 Id는 읽은 값으로 비교한다 (010과 10은 같은 Id다)
        var keyLocations = new Dictionary<string, string>();
        bool valid = true;
        foreach (var path in files)
        {
            string label = ToLabel(path);
            string[] fileLines = path == files[0] ? firstLines : File.ReadAllLines(path);
            //머리가 다르면 같은 칸 번호가 다른 값을 가리키게 된다
            if (fileLines.Length < 2 || fileLines[0] != merged[0] || fileLines[1] != merged[1])
            {
                valid = LogTableError(label, $"column names and types must be the same as {firstLabel}.");
                continue;
            }

            valid &= ValidateRows(label, fileLines, columns, keyType, keyLocations);
            for (int i = 2; i < fileLines.Length; i++)
                merged.Add(fileLines[i]);
        }

        if (!valid) return false;
        lines = merged.ToArray();
        return true;
    }

    private static bool ValidateHeader(string label, string[] lines)
    {
        if (lines.Length < 2)
            return LogTableError(label, "needs a column name line and a type line.");

        string[] columns = lines[0].Split('\t');
        string[] types = lines[1].Split('\t');
        if (columns.Length != types.Length)
            return LogTableError(label, $"has {columns.Length} column names but {types.Length} types.");

        string keyType = types[0];
        if (!KeyTypes.Contains(keyType))
            return LogTableError(label, $"first column {columns[0]} is the key. Its type must be {string.Join(", ", KeyTypes)} : {keyType}");
        return true;
    }

    private static bool ValidateRows(string label, string[] lines, string[] columns, string keyType, Dictionary<string, string> keyLocations)
    {
        bool valid = true;
        for (int i = 2; i < lines.Length; i++)
        {
            //줄 번호는 파일에서 보이는 번호(1부터)로 알린다
            int lineNumber = i + 1;
            string[] cells = lines[i].Split('\t');
            if (cells.Length != columns.Length)
            {
                valid = LogTableError(label, $"line {lineNumber} has {cells.Length} cells, but {columns.Length} columns are defined.");
                continue;
            }

            string key = cells[0];
            if (string.IsNullOrEmpty(key))
            {
                valid = LogTableError(label, $"line {lineNumber} has no {columns[0]}.");
                continue;
            }
            if (!TryNormalizeKey(keyType, key, out string normalized))
            {
                valid = LogTableError(label, $"line {lineNumber} {columns[0]} must be {keyType} : {key}");
                continue;
            }
            if (keyLocations.TryGetValue(normalized, out string firstLocation))
            {
                valid = LogTableError(label, $"line {lineNumber} {columns[0]} {key} is already used at {firstLocation}.");
                continue;
            }
            keyLocations[normalized] = $"{label} line {lineNumber}";
        }
        return valid;
    }

    //에러에 표 폴더 기준 경로를 적는다. 챕터 폴더마다 같은 이름의 파일이 있어서 이름만으로는 어느 파일인지 모른다
    private static string ToLabel(string path) => Path.GetRelativePath(TableDirectoryPath, path).Replace('\\', '/');

    //숫자 Id는 생성된 코드처럼 숫자로 읽어서 비교한다. 문자열 Id는 적힌 그대로 비교한다 (대소문자도 다른 Id다)
    private static bool TryNormalizeKey(string keyType, string key, out string normalized)
    {
        normalized = key;
        switch (keyType)
        {
            case "long":
                if (!long.TryParse(key, out long longKey)) return false;
                normalized = longKey.ToString();
                return true;
            case "int":
                if (!int.TryParse(key, out int intKey)) return false;
                normalized = intKey.ToString();
                return true;
        }
        return true;
    }

    //에러를 남기고 false를 돌려준다. 실패 지점에서 바로 return할 수 있게 bool을 돌려준다
    private static bool LogTableError(string label, string message)
    {
        Debug.LogError($"{label} : {message}");
        return false;
    }

    private static void CreateTable(string tableName, byte[] byteDatas)
    {
        string bytesDirectoryPath = Path.Combine(Application.dataPath, BytesDirectory);
        if(!Directory.Exists(bytesDirectoryPath))
            Directory.CreateDirectory(bytesDirectoryPath);
        string bytesPath = Path.Combine(bytesDirectoryPath, tableName + ".bytes");
        
        File.WriteAllBytes(bytesPath, byteDatas);
    }
    #endregion


    #region Types
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
    #endregion
}
