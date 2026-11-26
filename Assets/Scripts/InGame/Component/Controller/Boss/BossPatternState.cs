using System;
using System.Threading;
using Common.Template.FSM;
using Cysharp.Threading.Tasks;
using InGame.Component.Controller.Boss.Patterns;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component.Controller.Boss
{
    /// <summary>
    /// 지금 페이즈에서 패턴 하나를 골라 끝까지 한다. 패턴이 끝나거나 고를 패턴이 없으면 다음 상태(쉬기)로 넘어간다.
    /// 패턴은 커맨드를 차례로 기다리는 비동기 작업이라 상태 머신 밖에서 돈다. 다른 상태로 넘어가면 OnExit에서 취소한다
    /// </summary>
    public class BossPatternState : BossStateBase
    {
        #region Fields
        private readonly BossState _finishedState;
        //지금 패턴의 취소 토큰. 만든 이 상태가 취소하고 버린다. 실행하는 쪽은 버리지 않는다
        private CancellationTokenSource _patternCts;

        //지금 하는 패턴. 하는 중이 아니면 null이다
        public BossPattern CurrentPattern { get; private set; }
        #endregion

        #region Init
        public BossPatternState(StateMachine<BossState> stateMachine, ControllerBossAI controller, BossState finishedState)
            : base(stateMachine, controller)
        {
            _finishedState = finishedState;
        }
        #endregion

        #region LifeCycle
        public override void OnEnter()
        {
            base.OnEnter();

            var selector = Controller.PatternSelector;
            var context = Controller.CreatePatternContext();
            if (!selector.TrySelect(in context, out var pattern))
            {
                StateMachine.ChangeState(_finishedState);
                return;
            }

            selector.MarkStarted(pattern, context.Time);
            CurrentPattern = pattern;
            Controller.Context.SetPatternName(pattern.Name);
            _patternCts = new CancellationTokenSource();
            RunPattern(pattern, _patternCts).Forget();
        }

        public override void OnExit()
        {
            //패턴이 걸어둔 것을 거둔다. 끊긴 패턴은 자기가 누르던 이동 입력과 예고를 되돌리지 못한다
            CurrentPattern = null;
            Controller.StopMove();
            Controller.Context.SetTelegraph(AttackTelegraph.None);
            Controller.Context.SetPatternName(string.Empty);

            if (_patternCts == null) return;

            var cts = _patternCts;
            _patternCts = null;
            cts.Cancel();
            cts.Dispose();
        }
        #endregion

        #region Private
        /// <summary>
        /// 떼어 놓고 돌린 패턴이 어떻게 끝났는지 보는 곳이다. 끼어들기로 취소되었으면 넘긴 쪽이 이미 다음 상태를 정했다.
        /// 패턴이 예외를 던지면 로그를 남기고 다음 상태로 넘긴다. 넘기지 않으면 보스가 패턴 상태에 멈춘 채로 남는다
        /// </summary>
        private async UniTaskVoid RunPattern(BossPattern pattern, CancellationTokenSource cts)
        {
            var token = cts.Token;
            try
            {
                await pattern.Run(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, Controller);
            }

            //이미 다른 상태로 넘어간 뒤에 끝났으면 손대지 않는다
            if (_patternCts != cts) return;

            _patternCts = null;
            cts.Dispose();
            Controller.RequestNextIdleDuration(pattern.RecoveryDuration);
            StateMachine.ChangeState(_finishedState);
        }
        #endregion
    }
}
