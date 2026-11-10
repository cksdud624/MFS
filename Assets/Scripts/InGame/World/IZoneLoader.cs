using System.Threading;
using Cysharp.Threading.Tasks;

namespace InGame.World
{
    /// <summary>
    /// Zone 하나의 내용물(지형, 배경, 발판)을 불러오고 내린다. 지금은 프리팹으로 불러온다(PrefabZoneLoader).
    /// 나중에 베이크한 조명이 필요해져서 Zone을 Additive 씬으로 바꾸게 되면 이 구현만 바꾸고, 배치와 스트리밍은 그대로 둔다
    /// </summary>
    public interface IZoneLoader
    {
        /// <summary>Zone을 불러와 월드의 제자리(Zone 영역의 왼쪽 아래)에 놓는다. 내용물이 없으면 false</summary>
        UniTask<bool> Load(ZoneLayout.ZoneEntry zone, CancellationToken cancellationToken);

        void Unload(ZoneLayout.ZoneEntry zone);
    }
}
