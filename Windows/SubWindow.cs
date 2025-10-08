using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace QuickMate.Windows
{
    public class SubWindow : Window, IDisposable
    {
        private readonly Plugin plugin;

        // === F3設定 ===
        public Vector4 F3_TextColor { get; set; } = new(1.0f, 0.2f, 0.2f, 1.0f);   // 本文色 (赤)
        public Vector4 F3_OutlineColor { get; set; } = new(0f, 0f, 0f, 1.0f);      // 縁取り色
        public float F3_FontScale { get; set; } = 1.8f;                            // フォント倍率
        public float F3_OutlineThickness { get; set; } = 2.0f;                     // 縁取り太さ
        public float F3_OffsetX { get; set; } = 22.0f;                             // x補正
        public float F3_OffsetY { get; set; } = 0.0f;                              // y補正

        // === F4設定 ===
        public Vector4 F4_TextColor { get; set; } = new(0.2f, 0.8f, 1.0f, 1.0f);   // 本文色 (水色)
        public Vector4 F4_OutlineColor { get; set; } = new(0f, 0f, 0f, 1.0f);      // 縁取り色
        public float F4_FontScale { get; set; } = 1.4f;                            // フォント倍率
        public float F4_OutlineThickness { get; set; } = 1.5f;                     // 縁取り太さ
        public float F4_OffsetX { get; set; } = 22.0f;                             // x補正
        public float F4_OffsetY { get; set; } = 50.0f;                             // y補正（下方向）

		//ウィンドウ:コンストラクタ
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
		}

        public void Dispose() { }

        public override void Draw()
        {
            // === F3表示 ===
    	if (plugin.showF3Text)
            {
                DrawOverlay("F3 KEY PRESSED",
                    F3_TextColor,
                    F3_OutlineColor,
                    F3_FontScale,
                    F3_OutlineThickness,
                    F3_OffsetX,
                    F3_OffsetY);
            }

            // === F4表示 ===
    		if (plugin.showF4Text)
            {
                DrawOverlay("F4 KEY PRESSED",
                    F4_TextColor,
                    F4_OutlineColor,
                    F4_FontScale,
                    F4_OutlineThickness,
                    F4_OffsetX,
                    F4_OffsetY);
            }
        }

        private void DrawOverlay(string text, Vector4 textColor, Vector4 outlineColor,
                                 float fontScale, float outlineThickness,
                                 float offsetX, float offsetY)
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
            var textSize = ImGui.CalcTextSize(text) * fontScale;

            // === カメラ距離補正 ===
            if (Plugin.ClientState?.LocalPlayer?.Position is Vector3 camPos)
            {
                float distance = Vector3.Distance(worldPos, camPos);
                float correction = Math.Clamp(distance / 150f, 0f, 1.5f);
                screenPos.X += correction * 10f;
            }

            // === 中央補正 + 手動オフセット ===
            Vector2 drawPos = new(
                screenPos.X - textSize.X / 2 + offsetX,
                screenPos.Y - textSize.Y + offsetY
            );

            uint mainCol = ImGui.ColorConvertFloat4ToU32(textColor);
            uint outlineCol = ImGui.ColorConvertFloat4ToU32(outlineColor);
            int t = (int)Math.Ceiling(outlineThickness);

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