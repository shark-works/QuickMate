using System;
using System.Numerics;
using Lumina.Excel.Sheets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;

namespace QuickMate;

public class MainWindow : Window, IDisposable
{
    private readonly string ImagePath;
    private readonly Plugin plugin;

	public MainWindow(Plugin plugin, string ImagePath)
		: base("My Amazing Window##With a hidden ID", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
	{
		SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(375, 330),
			MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
		};

		this.ImagePath = ImagePath;
		this.plugin = plugin;
	}

	public void Dispose() { }

	public override void Draw()
	{
		ImGui.TextUnformatted($"The random config bool is {plugin.Configuration.SomePropertyToBeSavedAndWithADefault}");

		if (ImGui.Button("Show Settings"))
		{
			plugin.ToggleConfigUi();
		}

		ImGui.Spacing();

        ImGui.Separator();
        ImGui.Text("--- Hello Text Debug Info ---");
        ImGui.Text($"showHelloText: {plugin.showHelloText}");
        ImGui.Text($"helloTimer: {plugin.helloTimer:F2}");
        ImGui.Text($"LocalPlayer exists: {(Plugin.ClientState?.LocalPlayer != null)}");
        if (Plugin.ClientState?.LocalPlayer != null)
        {
            ImGui.Text($"Player Position Y: {Plugin.ClientState.LocalPlayer.Position.Y:F2}");
        }
        ImGui.Separator();

		using (var child = ImRaii.Child("SomeChildWithAScrollbar", Vector2.Zero, true))
		{
			if (child.Success)
			{
				ImGui.TextUnformatted("Have a Image:");
				var goatImage = Plugin.TextureProvider.GetFromFile(ImagePath).GetWrapOrDefault();
				if (goatImage != null)
				{
					using (ImRaii.PushIndent(55f))
					{
						ImGui.Image(goatImage.Handle, goatImage.Size);
					}
				}
				else
				{
					ImGui.TextUnformatted("Image not found.");
				}

				ImGuiHelpers.ScaledDummy(20.0f);

				var localPlayer = Plugin.ClientState.LocalPlayer;
				if (localPlayer == null)
				{
					ImGui.TextUnformatted("Our local player is currently not loaded.");
				} else {
                    if (!localPlayer.ClassJob.IsValid)
                    {
                        ImGui.TextUnformatted("Our current job is currently not valid.");
                    } else {
                        ImGui.TextUnformatted($"Our current job is ({localPlayer.ClassJob.RowId}) \"{localPlayer.ClassJob.Value.Abbreviation}\"");
                    }
                }

                var territoryId = Plugin.ClientState.TerritoryType;
                if (Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territoryRow))
                {
                    ImGui.TextUnformatted($"We are currently in ({territoryId}) \"{territoryRow.PlaceName.Value.Name}\"");
                }
                else
                {
                    ImGui.TextUnformatted("Invalid territory.");
                }
			}
		}

        if (plugin.showHelloText && Plugin.ClientState?.LocalPlayer != null)
        {
            var player = Plugin.ClientState.LocalPlayer;
            Vector3 worldPos = player.Position;

            worldPos.Y += 1.8f;

            bool worldToScreenSuccess = false;
            Vector2 screenPos = Vector2.Zero;

            if (Plugin.GameGui.WorldToScreen(worldPos, out screenPos))
            {
                worldToScreenSuccess = true;

                var drawList = ImGui.GetBackgroundDrawList();

                drawList.PushClipRect(ImGuiHelpers.MainViewport.Pos, ImGuiHelpers.MainViewport.Pos + ImGuiHelpers.MainViewport.Size, false);

                var text = "こんにちは";
                var textSize = ImGui.CalcTextSize(text);

                var textRenderPos = new Vector2(screenPos.X - textSize.X / 2, screenPos.Y - textSize.Y / 2);

                drawList.AddRectFilled(
                    new Vector2(textRenderPos.X - 4, textRenderPos.Y - 2),
                    new Vector2(textRenderPos.X + textSize.X + 4, textRenderPos.Y + textSize.Y + 2),
                    ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.5f))
                );

                drawList.AddText(
                    textRenderPos,
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)),
                    text
                );

                drawList.PopClipRect();
            }

            ImGui.Text($"WorldToScreen success: {worldToScreenSuccess}");
            ImGui.Text($"ScreenPos: X={screenPos.X:F2}, Y={screenPos.Y:F2}");
        }
    }
}