using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Story
{
    /// <summary>
    /// 시나리오나 퀘스트 명령 하나. 표 한 줄이 명령 하나가 된다.
    /// 실행하기 전에 Parse(칸 형식)와 Validate(앞뒤 정합성)를 거치고, 실행할 때는 해석해둔 값으로 Run만 한다.
    /// Run이 끝나야 다음 줄로 넘어가므로, 퀘스트 목표는 조건을 채울 때까지 Run을 끝내지 않는다.
    /// 각 명령이 Actor와 Param1~2를 어떻게 쓰는지는 명령 클래스의 summary에 적는다
    /// </summary>
    public abstract class StoryStep
    {
        #region Fields
        //어느 표의 줄인지 (Scenario, Quest). 에러와 로그에 붙인다
        public string Table { get; private set; }
        public long Id { get; private set; }
        public string Command { get; private set; }
        public StoryActor Actor { get; private set; }
        //플레이어에게 보여주는 글 (표의 Text 칸). 퀘스트의 목표 줄만 쓰고, 기다리는 동안 화면의 퀘스트 안내에 뜬다
        public string Text { get; private set; }

        //명령의 분류. 대부분이 내용이라 기본값으로 두고, 시작/종료/목표 명령만 다시 정한다
        public virtual StoryStepKind Kind => StoryStepKind.Content;
        //시나리오를 재생하는 줄인지. 목표 바로 뒤에 이런 줄이 오면 그 목표는 연출로 이어지는 목표다
        public virtual bool PlaysScenario => false;
        #endregion

        #region Init
        /// <summary>표 한 줄을 읽어 칸을 해석한다. 형식이 틀리거나 쓰지 않는 칸에 값이 있으면 에러를 남기고 false를 돌려준다</summary>
        public bool Init(StoryRow row)
        {
            Table = row.Table;
            Id = row.Id;
            Command = row.Command;
            Text = row.Text;

            var reader = new StoryParamReader(row);
            bool parsed = Parse(reader, out var actor);
            Actor = actor;
            return reader.CheckUnused() & CheckText() & parsed;
        }

        /// <summary>
        /// Text 칸은 목표 줄에만 적는다. 목표는 무엇을 해야 하는지를 화면의 퀘스트 안내에 띄워야 해서 꼭 적고,
        /// 다른 줄은 띄울 곳이 없으므로 적혀 있으면 다른 줄에 잘못 적은 것으로 본다
        /// </summary>
        private bool CheckText()
        {
            bool written = !string.IsNullOrEmpty(Text);
            if (Kind == StoryStepKind.Goal && !written)
            {
                LogError("Text is required. A goal shows it on the quest guide.");
                return false;
            }
            if (Kind != StoryStepKind.Goal && written)
            {
                LogError($"{Command} does not use Text. Only goals use it. Leave it empty : '{Text}'");
                return false;
            }
            return true;
        }
        #endregion

        #region Public
        /// <summary>
        /// 명령 이름으로 명령을 만들고 칸을 해석한다. 없는 이름이거나 칸이 틀리면 에러를 남기고 false.
        /// 시나리오와 퀘스트는 쓸 수 있는 명령이 달라서 이름 목록(creators)을 표마다 따로 넘긴다
        /// </summary>
        public static bool TryCreate(StoryRow row, IReadOnlyDictionary<string, Func<StoryStep>> creators, out StoryStep step)
        {
            step = null;
            if (!creators.TryGetValue(row.Command, out var create))
            {
                Debug.LogError($"{row} : unknown Command in {row.Table} table.");
                return false;
            }

            step = create();
            return step.Init(row);
        }

        /// <summary>
        /// 앞 줄들이 만든 상태(월드에 있는 대상, 화면)와 표 참조로 이 줄이 실행될 수 있는지 확인한다.
        /// 형식은 Parse에서 이미 확인했고, 대상이 월드에 있는지는 검사기가 분류(Kind)를 보고 먼저 확인한다.
        /// 그래서 여기서는 명령마다 다른 것만 본다. 따로 볼 것이 없는 명령은 다시 정하지 않는다
        /// </summary>
        public virtual void Validate(StoryValidator validator) { }

        /// <summary>
        /// 이 줄이 무대로 쓰는 Zone (대상을 세우거나 옮기는 곳). 없으면 0.
        /// 시나리오는 시작할 때 자기 줄들의 무대 Zone을 모두 올려둔다. 내려가 있는 Zone에 세운 대상은 꺼져서 연출이 그 줄에서 멈추기 때문이다.
        /// Zone 칸이 비어 있으면 Actor 표의 자리를 쓰는 명령이 있어서 표(book)를 같이 받는다
        /// </summary>
        public virtual long GetStageZone(StoryBook book) => 0;

        public abstract UniTask Run(StoryContext context, CancellationToken cancellationToken);

        public override string ToString() => $"{Table} {Id} {Command}";
        #endregion

        #region Protected
        /// <summary>
        /// 쓰는 칸을 읽어 필드에 담아둔다. 하나라도 틀리면 false.
        /// 여러 칸을 읽을 때는 &amp;&amp;가 아니라 &amp;로 잇는다. 앞 칸이 틀려도 뒤 칸까지 읽어야 틀린 칸이 한 번에 다 보인다
        /// </summary>
        protected abstract bool Parse(StoryParamReader reader, out StoryActor actor);

        /// <summary>이 줄의 에러를 실행 중에 남긴다. 검사로는 알 수 없는 것(에셋 누락, 사라진 대상)에 쓴다</summary>
        protected void LogError(string message) => Debug.LogError($"{this} : {message}");
        #endregion
    }
}
