using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ScouterX.Windows
{
    public class SubWindow : Window, IDisposable
    {
        private readonly Plugin plugin;

        private struct OverlaySettings
        {
            public Vector4 TextColor { get; set; }
            public Vector4 OutlineColor { get; set; }
            public float FontScale { get; set; }
            public float OutlineThickness { get; set; }

            public float FixedX { get; set; }
            public float FixedY { get; set; }

            public float OffsetX { get; set; }
            public float OffsetY { get; set; }
        }

        private OverlaySettings F1Settings { get; set; }
        private OverlaySettings F3Settings { get; set; }
        private OverlaySettings F4Settings { get; set; }

        public SubWindow(Plugin plugin)
            : base("Information Overlay##UniqueId",
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

            F1Settings = new OverlaySettings
            {
                TextColor = new(1.0f, 1.0f, 0.2f, 1.0f),
                OutlineColor = new(0f, 0f, 0f, 1.0f),
                FontScale = 1.5f,
                OutlineThickness = 1.0f,
                FixedX = 1500.0f,
                FixedY = 500.0f
            };

            F3Settings = new OverlaySettings
            {
                TextColor = new(1.0f, 0.2f, 0.2f, 1.0f),
                OutlineColor = new(0f, 0f, 0f, 1.0f),
                FontScale = 1.4f,
                OutlineThickness = 1.0f,
                OffsetX = 0.0f,                 //左右調整
                OffsetY = 0.0f                  //上下調整
            };

            F4Settings = new OverlaySettings
            {
                TextColor = new(0.2f, 0.8f, 1.0f, 1.0f),
                OutlineColor = new(0f, 0f, 0f, 1.0f),
                FontScale = 1.6f,
                OutlineThickness = 1.0f,
                OffsetX = 0.0f,
                OffsetY = -50.0f
            };
        }

        public void Dispose() { }

        public override void Draw()
        {
            if (Plugin.GameGui == null || Plugin.ClientState == null)
                return;

            if (plugin.showF1Text)
            {
                DrawFixedOverlay("F1 KEY PRESSED", F1Settings);
            }

            if (plugin.showF3Text)
            {
                DrawOverlay("F3 KEY PRESSED", F3Settings);
            }

            if (plugin.showF4Text)
            {
                DrawOverlay("F4 KEY PRESSED", F4Settings);
            }
        }

        // === 頭上描画 ===
        private void DrawOverlay(string text, OverlaySettings settings)
        {
            var player = Plugin.ClientState?.LocalPlayer;
            if (player == null)
                return;

            // === 頭上位置 ===
            Vector3 worldPos = player.Position;
            worldPos.Y += 1.7f;

            if (!Plugin.GameGui.WorldToScreen(worldPos, out var screenPos))
                return;

            var drawList = ImGui.GetBackgroundDrawList();

            // === カメラ補正 ===
            if (Plugin.ClientState?.LocalPlayer?.Position is Vector3 camPos)
            {
                float distance = Vector3.Distance(worldPos, camPos);
                float correction = Math.Clamp(distance / 150f, 0f, 1.5f);
                screenPos.X += correction * 10f;
            }

            ImGui.PushFont(ImGui.GetFont());              // 現在のフォントをプッシュ
            ImGui.SetWindowFontScale(settings.FontScale); // スケールを適用
            var textSize = ImGui.CalcTextSize(text);
            ImGui.SetWindowFontScale(1.0f);               // スケールをリセット
			ImGui.PopFont();                              // フォントをポップ

            // === 中央補正 + 手動オフセット ===
            Vector2 drawPos = new(
                screenPos.X - textSize.X / 2 + settings.OffsetX,
                screenPos.Y - textSize.Y + settings.OffsetY
            );

            DrawScaledText(drawList, text, drawPos, settings.TextColor, settings.OutlineColor, settings.FontScale, settings.OutlineThickness);
        }

        // === 固定描画 ===
        private void DrawFixedOverlay(string text, OverlaySettings settings)
        {
            var drawList = ImGui.GetBackgroundDrawList();
            Vector2 drawPos = new(settings.FixedX, settings.FixedY);
            DrawScaledText(drawList, text, drawPos, settings.TextColor, settings.OutlineColor, settings.FontScale, settings.OutlineThickness);
        }

        private void DrawScaledText(ImDrawListPtr drawList, string text, Vector2 pos,
                                    Vector4 color, Vector4 outline, float scale, float outlineThickness)
        {
            ImGui.PushFont(ImGui.GetFont());
            ImGui.SetWindowFontScale(scale);

            uint mainCol = ImGui.ColorConvertFloat4ToU32(color);
            uint outlineCol = ImGui.ColorConvertFloat4ToU32(outline);
            int t = (int)Math.Ceiling(outlineThickness);

            // 縁取り
            for (int dx = -t; dx <= t; dx++)
            {
                for (int dy = -t; dy <= t; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), new Vector2(pos.X + dx, pos.Y + dy), outlineCol, text);
                }
            }

            // 本体
            drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), pos, mainCol, text);

            ImGui.SetWindowFontScale(1.0f); // スケールをリセット
            ImGui.PopFont();
        }
    }
}