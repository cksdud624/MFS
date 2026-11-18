using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 생성된 .bytes를 게임과 같은 방식(한 줄씩 BinaryReader)으로 읽는다. Addressables를 거치지 않아서 Play 없이 읽힌다.
/// 이야기 검사 메뉴와 시나리오 툴처럼 에디터에서 표를 읽어야 하는 곳이 같이 쓴다
/// </summary>
public static class TableBytes
{
    #region Fields
    private const string BytesDirectory = "Assets/Generated/Table/";
    private const string BytesExtension = ".bytes";
    #endregion

    #region Public
    public static string GetPath(string tableName) => BytesDirectory + tableName + BytesExtension;

    /// <summary>표를 읽는다. 파일이 없으면 에러를 남기고 null을 돌려준다</summary>
    public static List<T> Read<T>(string tableName, Func<BinaryReader, T> create)
    {
        string path = GetPath(tableName);
        if (!File.Exists(path))
        {
            Debug.LogError($"{path} is missing. Run Table > Update Table first.");
            return null;
        }

        var datas = new List<T>();
        using var stream = new MemoryStream(File.ReadAllBytes(path));
        using var reader = new BinaryReader(stream);
        while (reader.BaseStream.Position < reader.BaseStream.Length)
            datas.Add(create(reader));
        return datas;
    }
    #endregion
}
