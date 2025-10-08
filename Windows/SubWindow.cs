using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace QuickMate.Windows
{
    public class SubWindow : Window, IDisposable
    {
        private readonly Plugin plugin;

        // === カスタマイズ可能設定 ===
        public Vector4 TextColor { get; set; } = new(1.0f, 0.2f, 0.2f, 1.0f);   // 本文色
        public Vector4 OutlineColor { get; set; } = new(0f, 0f, 0f, 1.0f);      // 縁取り色
        public float FontScale { get; set; } = 1.8f;                            // フォント倍率
        public float OutlineThickness { get; set; } = 2.0f;                     // 縁取り太さ

        // === 手動補正用オフセット ===
        public float OffsetX { get; set; } = 22.0f;   // x補正:右＋
        public float OffsetY { get; set; } = 0.0f;    // y補正:下＋

        public SubWindow(Plugin plugin)
            : base("QuickMate Warning Overlay##UniqueId",
                  ImGuiWindowFlags.NoDecoration |
                  ImGuiWindowFlags.NoBackground |
                  ImGuiWindowFlags.NoInputs |
                  ImGuiWindowFlags.NoNav |
                  ImGuiWindowFlags.NoSavedSettings |
                  ImGuiWindowFlags.NoFocusOnAppearing |
                  ImGuiWindowFlags.NoDocking)
        {
            this.plugin = plugin;
            RespectCloseHotkey = false;
            IsOpen = true;
        }

        public void Dispose() { }

        public override void Draw()
        {
            if (!plugin.showWarningText)
                return;

            var player = Plugin.ClientState?.LocalPlayer;
            if (player == null)
                return;

            // === 頭上位置 ===
            Vector3 worldPos = player.Position;
            worldPos.Y += 1.7f; // キャラクター頭上に補正

            if (!Plugin.GameGui.WorldToScreen(worldPos, out var screenPos))
                return;

            const string text = "WARNING";
            var drawList = ImGui.GetBackgroundDrawList();

            // === フォントスケールは AddText に直接は反映されないので CalcTextSize はそのまま使用 ===
            var textSize = ImGui.CalcTextSize(text) * FontScale;

            // === カメラ距離補正 (PeepingTom方式) ===
            if (Plugin.ClientState?.LocalPlayer?.Position is Vector3 camPos)
            {
                float distance = Vector3.Distance(worldPos, camPos);
                float correction = Math.Clamp(distance / 150f, 0f, 1.5f);
                screenPos.X += correction * 10f; // 右方向補正
            }

            // === 中央補正 + 手動オフセット ===
            Vector2 drawPos = new(
                screenPos.X - textSize.X / 2 + OffsetX,
                screenPos.Y - textSize.Y + OffsetY
            );

            uint mainCol = ImGui.ColorConvertFloat4ToU32(TextColor);
            uint outlineCol = ImGui.ColorConvertFloat4ToU32(OutlineColor);
            int t = (int)Math.Ceiling(OutlineThickness);

            // === 縁取り描画 ===
            for (int dx = -t; dx <= t; dx++)
            {
                for (int dy = -t; dy <= t; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    drawList.AddText(new Vector2(drawPos.X + dx, drawPos.Y + dy), outlineCol, text);
                }
            }

            // === 本文描画 ===
            drawList.AddText(drawPos, mainCol, text);
        }
    }
}