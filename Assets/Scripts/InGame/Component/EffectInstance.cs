using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace InGame.Component
{
    /// <summary>
    /// 이펙트 인스턴스 하나의 생명 주기를 스스로 관리한다.
    /// 재생이 끝나면 자기 자신을 파괴하므로 같은 이펙트가 겹쳐서 생성돼도 상관없다.
    /// </summary>
    public class EffectInstance : MonoBehaviour
    {
        private ParticleSystem[] _particles;

        /// <summary>
        /// direction은 이펙트의 진행 방향. 이펙트가 어느 쪽으로 그려지는지(꼬리가 뒤로 끌리는지 등)는
        /// 프리팹이 알아서 들고 있으므로 호출한 쪽에서 뒤집어 넘기지 않는다.
        /// duration이 0 이하면 파티클 수명대로 재생한다.
        /// follow가 true면 부모를 따라 움직이는 이펙트로 보고 파티클을 월드 기준으로 시뮬레이션한다.
        /// </summary>
        public void Play(Vector2 direction, float duration, bool follow = false)
        {
            _particles = GetComponentsInChildren<ParticleSystem>(true);

            ApplyDirection(direction);
            if (follow)
                ApplyWorldSimulation();

            foreach (var particle in _particles)
                particle.Play();

            Run(duration, this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>이펙트 중점을 축으로 진행 방향만큼 돌려준다</summary>
        private void ApplyDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= Mathf.Epsilon) return;

            //프리팹이 방향 처리를 직접 들고 있으면 그쪽에 맡긴다.
            //파티클은 Shape이나 회전까지 같이 뒤집어야 해서 프리팹마다 맞춰야 하는 부분이 있다
            var effectDirection = GetComponentInChildren<EffectDirection>(true);
            if (effectDirection != null)
            {
                effectDirection.SetDirection(direction);
                return;
            }

            //프리팹이 방향 처리를 안 들고 있을 때만 쓰는 기본 처리.
            //오른쪽(+X)을 기준으로 그려진 이펙트를 방향 각도만큼 회전시킨다
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            //왼쪽을 향하면 회전만으로는 위아래가 뒤집힌다.
            //스케일을 음수로 만들면 파티클이 같이 깨지므로 Y축 180도로 좌우만 뒤집는다
            transform.rotation = direction.x < 0f
                ? Quaternion.Euler(0f, 180f, 180f - angle)
                : Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>
        /// 따라다니는 이펙트는 파티클까지 같이 끌려오면 궤적이 남지 않으므로 월드 기준으로 시뮬레이션한다.
        /// 프리팹이 이미 World나 Custom을 지정했다면 그 의도를 존중해서 건드리지 않는다.
        /// </summary>
        private void ApplyWorldSimulation()
        {
            foreach (var particle in _particles)
            {
                var main = particle.main;
                if (main.simulationSpace == ParticleSystemSimulationSpace.Local)
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
            }
        }

        private async UniTask Run(float duration, CancellationToken cancellationToken)
        {
            //duration이 정해져 있으면 그 시간만큼만 뿜고, 이미 나온 파티클은 수명대로 사라지게 둔다
            if (duration > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: cancellationToken);

            foreach (var particle in _particles)
                particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);

            //루프 파티클도 방출만 멈추면 수명대로 사라진다
            await UniTask.WaitUntil(IsDead, cancellationToken: cancellationToken);
            Destroy(gameObject);
        }

        private bool IsDead()
        {
            foreach (var particle in _particles)
            {
                if (particle.IsAlive(false)) return false;
            }

            return true;
        }
    }
}
