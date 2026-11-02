using Bear.EventSystem;
using Config.Game;

namespace Game.Events
{
    public enum ActorSkinSelectionChangeReason
    {
        Applied,
        Cleared,
        GrantedAndApplied,
        Refresh
    }

    public sealed class ActorSkinSelectionChangedEvent : EventBase<SkinPart, ActorSkinSelectionChangeReason, int>
    {
        public SkinPart ChangedPart => Field1;
        public ActorSkinSelectionChangeReason Reason => Field2;
        public int Version => Field3;
    }
}
