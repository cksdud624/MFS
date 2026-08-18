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

        /// <summary>duration이 0 이하면 파티클 수명대로 재생한다</summary>
        public void Play(Vector2 direction, float duration)
        {
            _particles = GetComponentsInChildren<ParticleSystem>(true);

            ApplyDirection(direction);

            foreach (var particle in _particles)
                particle.Play();

            Run(duration, this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>이펙트 중점을 축으로 진행 방향만큼 돌려준다</summary>
        private void ApplyDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= Mathf.Epsilon) return;

            //오른쪽(+X)을 기준으로 그려진 이펙트를 방향 각도만큼 회전시킨다
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);

            //왼쪽을 향하면 회전만으로는 위아래가 뒤집히므로 Y를 한 번 더 뒤집어 세워둔다
            var scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x);
            scale.y = Mathf.Abs(scale.y) * (direction.x < 0f ? -1f : 1f);
            transform.localScale = scale;
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
