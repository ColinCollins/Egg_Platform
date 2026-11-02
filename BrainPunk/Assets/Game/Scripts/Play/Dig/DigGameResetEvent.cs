using Bear.EventSystem;
using Game.Play;

namespace Game.Events
{
    /// <summary>Dig 专属手动/失败重置请求，禁止转发到主线 GameResetEvent。</summary>
    public sealed class DigGameResetEvent : EventBase<GameResetType>
    {
        /// <summary>区分手动重置和死亡表现完成后的失败重置。</summary>
        public GameResetType Type => Field1;
    }
}
