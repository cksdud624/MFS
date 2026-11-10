using System.Threading;
using Cysharp.Threading.Tasks;
using static Common.GameDefine;

namespace InGame.Story.Scenario.Steps
{
    /// <summary>
    /// FadeOut / FadeIn : 화면을 검게 덮거나(FadeOut) 걷어낸다(FadeIn). 다 바뀌면 다음 줄로 넘어간다.
    /// 걸리는 시간은 표에서 정하지 않고 늘 ScreenFadeDuration을 쓴다. 바꾸려면 GameDefine의 값을 고친다.
    /// 대화창은 덮개보다 위에 그려지므로 FadeOut 뒤에 Dialogue를 두면 검은 화면 위에 대사가 나온다.
    /// 시나리오가 끝나면 퀘스트 목표가 이어지므로, 덮은 채로 시나리오가 끝나면 정합성 검사에서 막는다.
    /// 쓰는 칸 없음
    /// </summary>
    public class FadeStep : StoryStep
    {
        #region Fields
        //true면 덮고(FadeOut) false면 걷어낸다(FadeIn). 두 명령이 이 값만 달라서 클래스 하나로 둔다
        private readonly bool _cover;
        #endregion

        #region Init
        public FadeStep(bool cover)
        {
            _cover = cover;
        }
        #endregion

        #region Public
        public override void Validate(StoryValidator validator) => validator.SetScreenCovered(_cover);

        public override async UniTask Run(StoryContext context, CancellationToken cancellationToken)
        {
            //페이드 프리팹은 에셋이라 정합성 검사로는 잡을 수 없다. 만들지 않았으면 여기서 걸린다
            if (context.ScreenFade == null)
            {
                LogError($"{UIScreenFadeView} is missing. Build it with the editor menu UI > Build Screen Fade View.");
                return;
            }

            await context.ScreenFade.Fade(_cover ? 1f : 0f, ScreenFadeDuration, cancellationToken);
        }
        #endregion

        #region Protected
        protected override bool Parse(StoryParamReader reader, out StoryActor actor)
        {
            actor = StoryActor.None;
            return true;
        }
        #endregion
    }
}
