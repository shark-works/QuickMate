using ImGuiNET;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;

namespace QuickMate
{
    public abstract class Overlay
    {
        public IGameObject? TargetObject { get; protected set; }
        public bool IsEnabled { get; set; } = true;
        public Vector2 ScreenOffset { get; set; } = Vector2.Zero;

        public abstract void Draw(ImDrawListPtr drawList);

        public virtual bool ShouldDraw()
        {
            return IsEnabled && TargetObject != null && TargetObject.GameObjectId != 0;
        }
    }
}