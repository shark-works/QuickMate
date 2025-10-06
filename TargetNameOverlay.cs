using ImGuiNET;
using System.Numerics;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Utility;
using Dalamud.Game.ClientState.Objects.Types;

namespace QuickMate
{
    public class TargetNameOverlay : Overlay
    {
        private readonly IGameGui _gameGui;

        public TargetNameOverlay(IGameGui gameGui)
        {
            _gameGui = gameGui;
        }

        public void UpdateTarget(IGameObject? target)
        {
            TargetObject = target;
        }

        public override bool ShouldDraw()
        {
            return base.ShouldDraw() && TargetObject is IBattleChara;
        }

        public override void Draw(ImDrawListPtr drawList)
        {
            if (!ShouldDraw() || TargetObject == null) return;

            if (_gameGui.WorldToScreen(TargetObject.Position, out var screenPos))
            {
                screenPos += ScreenOffset;

                var name = TargetObject.Name.TextValue;
                var textColor = ImGui.GetColorU32(new Vector4(1, 1, 1, 1));

                var textSize = ImGui.CalcTextSize(name);
                var textPos = new Vector2(screenPos.X - textSize.X / 2, screenPos.Y - textSize.Y - 5);

                drawList.AddText(textPos, textColor, name);
            }
        }
    }
}