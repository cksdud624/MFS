using System.Globalization;
using UnityEngine;

namespace InGame.Story
{
    /// <summary>
    /// 시나리오나 퀘스트 한 줄의 Actor, Zone, Param1~2를 명령이 원하는 타입으로 읽는다.
    /// Param 칸은 표에서 문자열이라 생성기가 타입을 확인해주지 않으므로 여기서 확인하고, 틀리면 어느 칸이 왜 틀렸는지 에러로 남긴다.
    /// 읽은 칸을 기록해두고, 명령이 쓰지 않는 칸에 값이 적혀 있으면 그것도 에러로 본다. 다른 명령의 칸 배치로 잘못 적은 줄을 잡기 위해서다.
    /// Note 칸은 설계자 메모라 읽지 않고, 쓰지 않는 칸 검사에서도 뺀다
    /// </summary>
    public class StoryParamReader
    {
        #region Fields
        private const int ParamCount = 2;

        private readonly StoryRow _row;
        //Param1~2. 번호로 꺼내기 쉽게 배열로 들고 있는다
        private readonly string[] _params;
        //명령이 Actor 칸을 읽었는지
        private bool _actorRead;
        //명령이 Zone 칸을 읽었는지
        private bool _zoneRead;
        //명령이 읽은 Param. 인덱스가 Param 번호다 (0은 쓰지 않는다)
        private readonly bool[] _paramRead = new bool[ParamCount + 1];
        #endregion

        #region Init
        public StoryParamReader(StoryRow row)
        {
            _row = row;
            _params = new[] { row.Param1, row.Param2 };
        }
        #endregion

        #region Public
        /// <summary>
        /// 대상을 읽는다. required면 빈칸일 때 에러다. Player 또는 Actor 표의 번호(10001)를 적는다.
        /// 번호가 Actor 표에 있는지는 표끼리 맞춰봐야 해서 검사기가 본다
        /// </summary>
        public bool ReadActor(bool required, out StoryActor actor)
        {
            _actorRead = true;
            actor = StoryActor.None;

            if (string.IsNullOrEmpty(_row.Actor))
                return !required || LogError("Actor is required.");
            if (StoryActor.TryParse(_row.Actor, out actor)) return true;
            return LogError($"Actor must be {StoryActor.PlayerName} or an Actor Id such as 10001 : '{_row.Actor}'");
        }

        /// <summary>
        /// 위치 칸의 기준 Zone을 읽는다. 위치는 그 Zone의 왼쪽 아래를 0,0으로 보는 로컬 좌표다.
        /// required면 빈칸(0)일 때 에러다. Zone 표에 있는 번호인지는 검사기가 본다
        /// </summary>
        public bool ReadZone(bool required, out long zone)
        {
            _zoneRead = true;
            zone = _row.Zone;
            if (zone != 0 || !required) return true;
            return LogError("Zone is required. Positions are local to the zone (its bottom-left is 0,0).");
        }

        public bool ReadLong(int number, string name, out long value)
        {
            string raw = Take(number);
            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return true;
            return Fail(number, name, "long", raw);
        }

        public bool ReadFloat(int number, string name, out float value)
        {
            string raw = Take(number);
            if (TryParseFloat(raw, out value)) return true;
            return Fail(number, name, "float", raw);
        }

        /// <summary>빈칸이면 기본값을 쓴다. 값이 적혀 있으면 형식은 확인한다</summary>
        public bool ReadOptionalFloat(int number, string name, float defaultValue, out float value)
        {
            value = defaultValue;
            if (IsWritten(number)) return ReadFloat(number, name, out value);

            Take(number);
            return true;
        }

        public bool ReadVector2(int number, string name, out Vector2 value)
        {
            value = Vector2.zero;
            string raw = Take(number);
            //생성기와 같이 쉼표와 세미콜론을 둘 다 받는다
            string[] items = raw.Split(',', ';');
            if (items.Length == 2 && TryParseFloat(items[0], out float x) && TryParseFloat(items[1], out float y))
            {
                value = new Vector2(x, y);
                return true;
            }
            return Fail(number, name, "Vector2 (x,y)", raw);
        }

        /// <summary>빈칸이면 기본값을 쓴다. 값이 적혀 있으면 형식은 확인한다</summary>
        public bool ReadOptionalVector2(int number, string name, Vector2 defaultValue, out Vector2 value)
        {
            value = defaultValue;
            if (IsWritten(number)) return ReadVector2(number, name, out value);

            Take(number);
            return true;
        }

        public bool ReadString(int number, string name, out string value)
        {
            value = Take(number);
            if (!string.IsNullOrEmpty(value)) return true;
            return Fail(number, name, "string", value);
        }

        /// <summary>칸에 값이 적혀 있는지. 빈칸과 0을 다르게 다뤄야 하는 명령이 쓴다 (위치를 비우면 Actor 표의 자리)</summary>
        public bool IsWritten(int number) => !string.IsNullOrEmpty(Peek(number));

        /// <summary>명령이 쓰지 않는 칸에 값이 적혀 있으면 에러로 남긴다. 해석이 끝난 뒤에 부른다</summary>
        public bool CheckUnused()
        {
            bool valid = true;
            if (!_actorRead && !string.IsNullOrEmpty(_row.Actor))
                valid = LogError($"{_row.Command} does not use Actor. Leave it empty : '{_row.Actor}'");
            if (!_zoneRead && _row.Zone != 0)
                valid = LogError($"{_row.Command} does not use Zone. Leave it empty : {_row.Zone}");

            for (int number = 1; number <= ParamCount; number++)
            {
                if (_paramRead[number] || !IsWritten(number)) continue;
                valid = LogError($"{_row.Command} does not use Param{number}. Leave it empty : '{Peek(number)}'");
            }
            return valid;
        }
        #endregion

        #region Private
        private string Peek(int number) => _params[number - 1] ?? string.Empty;

        private string Take(int number)
        {
            _paramRead[number] = true;
            return Peek(number);
        }

        //표의 숫자는 항상 소수점을 .으로 적으므로 PC 지역 설정과 상관없이 읽는다
        private static bool TryParseFloat(string raw, out float value)
            => float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private bool Fail(int number, string name, string type, string raw)
            => LogError($"Param{number} ({name}) must be {type} : '{raw}'");

        //에러를 남기고 false를 돌려준다. 실패 지점에서 바로 return할 수 있게 bool을 돌려준다
        private bool LogError(string message)
        {
            Debug.LogError($"{_row} : {message}");
            return false;
        }
        #endregion
    }
}
