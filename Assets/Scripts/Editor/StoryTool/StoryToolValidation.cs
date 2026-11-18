using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace StoryTool
{
    /// <summary>검사에 걸린 줄 하나. 어느 줄인지는 StoryLine.Key와 같은 모양으로 든다</summary>
    [Serializable]
    public class StoryToolError
    {
        public string LineKey;
        public string Message;
    }

    /// <summary>
    /// 저장한 표를 게임이 읽는 .bytes로 갱신하고(Update Table) 이야기 검사(Validate Story)를 돌려서, 걸린 줄을 모아 돌려준다.
    /// 검사는 메뉴와 같은 것을 그대로 쓴다. 검사기와 명령 클래스가 에러를 콘솔에 남기므로, 도는 동안 콘솔에 남는 에러를 받아서 어느 줄인지 읽어낸다.
    /// 에러를 따로 모으게 검사기를 고치지 않은 것은, 에러를 남기는 곳이 검사기, 칸 읽기, 명령 클래스로 나뉘어 있어서 모두 고쳐야 하기 때문이다
    /// </summary>
    public static class StoryToolValidation
    {
        #region Fields
        //검사 에러는 "표 이름 줄Id 명령 : 내용" 모양이다 (Scenario 101020 Spawn : ...). Actor와 Talk 표는 명령이 없다 (Actor 10001 : ...)
        private static readonly Regex LineErrorPattern = new(@"^(?<table>Scenario|Quest|Actor|Talk) (?<id>\d+)(?: \w+)? : (?<message>.+)$", RegexOptions.Singleline);
        #endregion

        #region Public
        /// <summary>표를 갱신하고 검사한다. 통과했으면 true. 줄을 알 수 있는 에러는 errors에 담고, 나머지는 콘솔에만 남는다</summary>
        public static bool Run(List<StoryToolError> errors)
        {
            errors.Clear();

            void Capture(string condition, string stackTrace, LogType type)
            {
                if (type != LogType.Error) return;

                var match = LineErrorPattern.Match(condition);
                if (!match.Success) return;
                errors.Add(new StoryToolError
                {
                    LineKey = $"{match.Groups["table"].Value}:{match.Groups["id"].Value}",
                    Message = match.Groups["message"].Value,
                });
            }

            Application.logMessageReceived += Capture;
            try
            {
                //표를 갱신하지 못했으면 검사는 저장하기 전의 .bytes를 보게 되므로 하지 않는다
                return TableUpdater.Run() && StoryValidatorMenu.Run();
            }
            finally
            {
                Application.logMessageReceived -= Capture;
            }
        }
        #endregion
    }
}
