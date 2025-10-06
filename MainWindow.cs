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
						ImGui.Image(goatImage!.Handle, goatImage.Size);
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
                        ImGui.TextUnformatted($"Our current job is ({localPlayer.ClassJob.RowId}) \"{localPlayer.ClassJob.Value!.Abbreviation}\"");
                    }
                }
			}
		}
    }
}