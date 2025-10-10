using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ScouterX.Windows
{
    public class SubWindow : Window, IDisposable
    {
		private readonly Plugin plugin;

		private enum Alignment{ Left, Center, Right }

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
			public Alignment TextAlign { get; set; }
        }

        private OverlaySettings F1Settings { get; set; }
        private OverlaySettings F3Settings { get; set; }
        private OverlaySettings F4Settings { get; set; }
		private OverlaySettings F5Settings { get; set; }

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
                FixedY = 500.0f,
				TextAlign = Alignment.Left
            };

            F3Settings = new OverlaySettings
            {
                TextColor = new(1.0f, 0.2f, 0.2f, 1.0f),
                OutlineColor = new(0f, 0f, 0f, 1.0f),
                FontScale = 1.4f,
                OutlineThickness = 1.0f,
                OffsetX = 0.0f,                 //左右調整はここ
                OffsetY = 0.0f,                 //上下調整はここ
				TextAlign = Alignment.Center
            };

			F4Settings = new OverlaySettings
			{
				TextColor = new(0.2f, 0.8f, 1.0f, 1.0f),
				OutlineColor = new(0f, 0f, 0f, 1.0f),
				FontScale = 1.6f,
				OutlineThickness = 1.0f,
				OffsetX = 0.0f,
				OffsetY = -50.0f,
				TextAlign = Alignment.Center
			};

			F5Settings = new OverlaySettings
            {
                TextColor = new(1.0f, 1.0f, 1.0f, 1.0f),
                OutlineColor = new(0f, 0f, 0f, 1.0f),
                FontScale = 1.8f,
                OutlineThickness = 1.0f,
                FixedX = 1000.0f,
                FixedY = 700.0f,
                TextAlign = Alignment.Center
            };
        }

		public void Dispose() { }

        public override void Draw()
		{

            if (Plugin.GameGui == null || Plugin.ClientState == null)
                return;

			//F1
			if (plugin.showF1Text)
			{
				DrawFixedOverlay("F1 KEY PRESSED", F1Settings);
			}

			//F3
            if (plugin.showF3Text)
            {
                DrawOverlay("F3 KEY PRESSED", F3Settings);
            }

			//F4
			if (plugin.showF4Text)
			{
				DrawOverlay("F4 KEY PRESSED", F4Settings);
			}

			//F5
			if (plugin.showF5Timer)
            {
                TimeSpan time = TimeSpan.FromSeconds(plugin.f5Remaining);
                string timeText = $"{time.Minutes:D2}:{time.Seconds:D2}";
				var color = new Vector4(1f, 0.2f, 0.2f, 1f);

    			if (plugin.f5Remaining <= 0f)
        			color = new Vector4(1f, 1f, 1f, 1f); // 白
    			else if (plugin.f5Remaining <= 5f)
        			color = new Vector4(1f, 1f, 0.3f, 1f); // 黄色
    			else if (plugin.f5Remaining <= 30f)
        			color = new Vector4(1f, 0.6f, 0.1f, 1f); // オレンジ

    			var settings = F5Settings;
    			settings.TextColor = color;
    			F5Settings = settings;

    			DrawFixedOverlay(timeText, F5Settings);
            }
            else
            {
    			var settings = F5Settings;
    			settings.TextColor = new Vector4(1f, 1f, 1f, 1f); // 白にリセット
    			DrawFixedOverlay("00:00", settings);
            }
        }

        // === 頭上描画 ===
        private void DrawOverlay(string text, OverlaySettings settings)
        {
            var player = Plugin.ClientState?.LocalPlayer;
			if (player == null)
				return;

            //キャラクター係数
            Vector3 worldPos = player.Position;
            worldPos.Y += 1.7f;

            if (!Plugin.GameGui.WorldToScreen(worldPos, out var screenPos))
                return;

            var drawList = ImGui.GetBackgroundDrawList();

			//カメラ距離補正
			if (Plugin.ClientState?.LocalPlayer?.Position is Vector3 camPos)
			{
				float distance = Vector3.Distance(worldPos, camPos);
				float correction = Math.Clamp(distance / 150f, 0f, 1.5f);
				screenPos.X += correction * 10f;
			}

            ImGui.PushFont(ImGui.GetFont());
            ImGui.SetWindowFontScale(settings.FontScale);
            var textSize = ImGui.CalcTextSize(text);
            ImGui.SetWindowFontScale(1.0f);
			ImGui.PopFont();

            float x = screenPos.X;
            switch (settings.TextAlign)
            {
                case Alignment.Left:
                    x = screenPos.X;
                    break;
                case Alignment.Center:
                    x = screenPos.X - textSize.X / 2;
                    break;
                case Alignment.Right:
                    x = screenPos.X - textSize.X;
                    break;
            }
            Vector2 drawPos = new(
                x + settings.OffsetX,
                screenPos.Y - textSize.Y + settings.OffsetY
            );

            DrawScaledText(drawList, text, drawPos, settings.TextColor, settings.OutlineColor, settings.FontScale, settings.OutlineThickness);
        }

		// === 固定描画 ===
		private void DrawFixedOverlay(string text, OverlaySettings settings)
		{
			var drawList = ImGui.GetBackgroundDrawList();

			ImGui.PushFont(ImGui.GetFont());
			ImGui.SetWindowFontScale(settings.FontScale);
			var textSize = ImGui.CalcTextSize(text);
			ImGui.SetWindowFontScale(1.0f);
			ImGui.PopFont();

			float x = settings.FixedX;
			switch (settings.TextAlign)
			{
				case Alignment.Left:
					break;
				case Alignment.Center:
					x -= textSize.X / 2;
					break;
				case Alignment.Right:
					x -= textSize.X;
					break;
			}

			Vector2 drawPos = new(x, settings.FixedY);
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

            for (int dx = -t; dx <= t; dx++)
            {
                for (int dy = -t; dy <= t; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), new Vector2(pos.X + dx, pos.Y + dy), outlineCol, text);
                }
            }

            drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), pos, mainCol, text);

            ImGui.SetWindowFontScale(1.0f);
            ImGui.PopFont();
        }
    }
}