using System;
using System.Globalization;
using Generated.Table;
using UnityEngine;

namespace InGame.Component.Controller.Boss.Commands
{
    /// <summary>
    /// BossCommand 표 한 줄의 Param1~2를 커맨드가 원하는 타입으로 읽는다.
    /// Param 칸은 표에서 문자열이라 생성기가 타입을 확인해주지 않으므로 여기서 확인하고, 틀리면 어느 칸이 왜 틀렸는지 에러로 남긴다.
    /// 읽은 칸을 기록해두고, 커맨드가 쓰지 않는 칸에 값이 적혀 있으면 그것도 에러로 본다. 다른 커맨드의 칸 배치로 잘못 적은 줄을 잡기 위해서다
    /// </summary>
    public class BossParamReader
    {
        #region Fields
        private const int ParamCount = 2;

        private readonly BossCommandData _data;
        //Param1~2. 번호로 꺼내기 쉽게 배열로 들고 있는다
        private readonly string[] _params;
        //커맨드가 읽은 Param. 인덱스가 Param 번호다 (0은 쓰지 않는다)
        private readonly bool[] _paramRead = new bool[ParamCount + 1];
        #endregion

        #region Init
        public BossParamReader(BossCommandData data)
        {
            _data = data;
            _params = new[] { data.Param1, data.Param2 };
        }
        #endregion

        #region Public
        /// <summary>0보다 큰 실수를 읽는다. 거리와 시간은 0이면 아무것도 하지 않는 줄이 되므로 빈칸도 에러로 본다</summary>
        public bool ReadPositiveFloat(int number, string name, out float value)
        {
            string raw = Take(number);
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0f) return true;
            return Error($"Param{number} ({name}) must be a number greater than 0 : '{raw}'");
        }

        /// <summary>비어 있지 않은 글자를 읽는다</summary>
        public bool ReadText(int number, string name, out string value)
        {
            value = Take(number);
            if (!string.IsNullOrEmpty(value)) return true;
            return Error($"Param{number} ({name}) is required.");
        }

        /// <summary>enum 이름을 읽는다. 대소문자까지 같아야 한다. 숫자로 적은 값은 받지 않는다</summary>
        public bool ReadEnum<T>(int number, string name, out T value) where T : struct, Enum
        {
            string raw = Take(number);
            //Enum.TryParse는 "3" 같은 숫자도 받아서 없는 값이 들어갈 수 있다. 정의된 이름인지 따로 본다
            if (Enum.TryParse(raw, false, out value) && Enum.IsDefined(typeof(T), raw)) return true;
            return Error($"Param{number} ({name}) must be one of {string.Join(", ", Enum.GetNames(typeof(T)))} : '{raw}'");
        }

        /// <summary>커맨드가 읽지 않은 칸에 값이 적혀 있으면 에러를 남긴다. 모든 칸을 확인한 뒤 결과를 돌려준다</summary>
        public bool CheckUnused()
        {
            bool valid = true;
            for (int number = 1; number <= ParamCount; number++)
            {
                if (_paramRead[number] || string.IsNullOrEmpty(_params[number - 1])) continue;
                valid = Error($"{_data.Command} does not use Param{number}. Leave it empty : '{_params[number - 1]}'");
            }
            return valid;
        }

        /// <summary>이 줄의 에러를 남기고 false를 돌려준다. 실패 지점에서 바로 return할 수 있게 bool을 돌려준다</summary>
        public bool Error(string message)
        {
            Debug.LogError($"BossCommand {_data.Id} ({_data.Command}) : {message}");
            return false;
        }
        #endregion

        #region Private
        private string Take(int number)
        {
            _paramRead[number] = true;
            return _params[number - 1];
        }
        #endregion
    }
}
