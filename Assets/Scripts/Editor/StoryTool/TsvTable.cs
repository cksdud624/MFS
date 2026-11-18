using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace StoryTool
{
    /// <summary>
    /// tsv 파일 하나를 읽어서 칸 단위로 고치고 다시 쓴다. 첫 줄은 컬럼 이름, 둘째 줄은 타입이고 그 아래가 데이터다.
    /// 고치지 않은 칸은 읽은 글자 그대로 다시 쓰고 줄바꿈 방식과 BOM도 파일의 것을 지킨다. 그래야 저장했을 때 git diff에 고친 줄만 남는다.
    /// 도메인 리로드와 Undo를 넘어서 남아야 해서 Unity가 직렬화할 수 있는 모양으로 둔다
    /// </summary>
    [Serializable]
    public class TsvTable
    {
        #region Types
        //Unity는 배열의 배열을 직렬화하지 못해서 줄을 클래스로 감싼다
        [Serializable]
        private class Row
        {
            public string[] Cells;
        }
        #endregion

        #region Fields
        private const char Separator = '\t';
        //컬럼 이름 줄과 타입 줄
        private const int HeaderLineCount = 2;
        private const string WindowsNewLine = "\r\n";
        private const string UnixNewLine = "\n";

        //표 이름 (Scenario). 챕터 폴더로 나눈 표는 여러 파일이 같은 이름을 쓴다
        [SerializeField] private string _name;
        [SerializeField] private string _path;
        [SerializeField] private string[] _columns;
        [SerializeField] private string[] _types;
        [SerializeField] private List<Row> _rows = new();
        [SerializeField] private string _newLine = WindowsNewLine;
        [SerializeField] private bool _hasByteOrderMark;
        //파일이 줄바꿈으로 끝났는지. 다시 쓸 때 마지막 줄이 diff에 걸리지 않게 그대로 둔다
        [SerializeField] private bool _endsWithNewLine;
        //읽은 뒤에 고친 칸이 있는지
        [SerializeField] private bool _isDirty;
        //읽었을 때 파일의 수정 시각. 툴 밖에서 파일이 바뀌었는지 알아볼 때 쓴다
        [SerializeField] private long _loadedWriteTime;

        public string Name => _name;
        public string Path => _path;
        public IReadOnlyList<string> Columns => _columns;
        public int RowCount => _rows.Count;
        public bool IsDirty => _isDirty;
        public bool IsChangedOnDisk => !File.Exists(_path) || File.GetLastWriteTimeUtc(_path).Ticks != _loadedWriteTime;
        #endregion

        #region Public
        /// <summary>파일을 읽는다. 머리 두 줄이 없으면 에러를 남기고 false</summary>
        public static bool TryLoad(string name, string path, out TsvTable table)
        {
            table = new TsvTable { _name = name, _path = path };
            return table.Read();
        }

        public int FindColumn(string column) => Array.IndexOf(_columns, column);

        /// <summary>칸의 글자. 없는 컬럼이거나 그 줄에 칸이 모자라면 빈 문자열이다 (칸 수가 틀린 줄은 Update Table이 에러로 알린다)</summary>
        public string Get(int row, string column)
        {
            int index = FindColumn(column);
            var cells = _rows[row].Cells;
            return index >= 0 && index < cells.Length ? cells[index] : string.Empty;
        }

        /// <summary>칸을 고친다. 값이 실제로 바뀌었으면 true</summary>
        public bool Set(int row, string column, string value)
        {
            int index = FindColumn(column);
            var cells = _rows[row].Cells;
            if (index < 0 || index >= cells.Length || cells[index] == value) return false;

            cells[index] = value;
            _isDirty = true;
            return true;
        }

        /// <summary>그 컬럼의 값이 value인 첫 줄. 없으면 -1</summary>
        public int FindRow(string column, string value)
        {
            int index = FindColumn(column);
            if (index < 0) return -1;

            for (int row = 0; row < _rows.Count; row++)
            {
                var cells = _rows[row].Cells;
                if (index < cells.Length && cells[index] == value) return row;
            }
            return -1;
        }

        /// <summary>index 자리에 빈 줄을 끼운다. 그 뒤의 줄은 한 칸씩 밀린다. 끝에 붙이려면 RowCount를 넘긴다</summary>
        public void InsertRow(int index)
        {
            var cells = new string[_columns.Length];
            Array.Fill(cells, string.Empty);
            _rows.Insert(index, new Row { Cells = cells });
            _isDirty = true;
        }

        public void RemoveRow(int index)
        {
            _rows.RemoveAt(index);
            _isDirty = true;
        }

        /// <summary>두 줄의 내용을 통째로 맞바꾼다</summary>
        public void SwapRows(int a, int b)
        {
            (_rows[a], _rows[b]) = (_rows[b], _rows[a]);
            _isDirty = true;
        }

        public void Save()
        {
            var builder = new StringBuilder();
            builder.Append(string.Join(Separator, _columns)).Append(_newLine);
            builder.Append(string.Join(Separator, _types));
            foreach (var row in _rows)
                builder.Append(_newLine).Append(string.Join(Separator, row.Cells));
            if (_endsWithNewLine)
                builder.Append(_newLine);

            File.WriteAllText(_path, builder.ToString(), new UTF8Encoding(_hasByteOrderMark));
            _isDirty = false;
            _loadedWriteTime = File.GetLastWriteTimeUtc(_path).Ticks;
        }
        #endregion

        #region Private
        private bool Read()
        {
            byte[] bytes = File.ReadAllBytes(_path);
            byte[] mark = Encoding.UTF8.GetPreamble();
            _hasByteOrderMark = bytes.Length >= mark.Length && bytes[0] == mark[0] && bytes[1] == mark[1] && bytes[2] == mark[2];
            int offset = _hasByteOrderMark ? mark.Length : 0;
            string text = Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);

            _newLine = text.Contains(WindowsNewLine) ? WindowsNewLine : UnixNewLine;
            _endsWithNewLine = text.EndsWith(UnixNewLine);
            var lines = new List<string>(text.Split('\n'));
            //줄바꿈으로 끝난 파일은 마지막에 빈 조각이 하나 남는다. 데이터 줄이 아니다
            if (_endsWithNewLine)
                lines.RemoveAt(lines.Count - 1);
            if (lines.Count < HeaderLineCount)
            {
                Debug.LogError($"{_path} needs a column name line and a type line.");
                return false;
            }

            _columns = SplitLine(lines[0]);
            _types = SplitLine(lines[1]);
            _rows.Clear();
            for (int i = HeaderLineCount; i < lines.Count; i++)
                _rows.Add(new Row { Cells = SplitLine(lines[i]) });

            _isDirty = false;
            _loadedWriteTime = File.GetLastWriteTimeUtc(_path).Ticks;
            return true;
        }

        private static string[] SplitLine(string line) => line.TrimEnd('\r').Split(Separator);
        #endregion
    }
}
