using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace QuickMate.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly string goatImagePath;
    private readonly Plugin plugin;

	public MainWindow(Plugin plugin, string goatImagePath)
		: base("My Amazing Window##With a hidden ID", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
	{
		SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(375, 330),
			MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
		};

		this.goatImagePath = goatImagePath;
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

		using (var child = ImRaii.Child("SomeChildWithAScrollbar", Vector2.Zero, true))
		{
			if (child.Success)
			{
				ImGui.TextUnformatted("Have a goat:");
				var goatImage = Plugin.TextureProvider.GetFromFile(goatImagePath).GetWrapOrDefault();
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
					return;
				}

				if (!localPlayer.ClassJob.IsValid)
				{
					ImGui.TextUnformatted("Our current job is currently not valid.");
					return;
				}

				ImGui.TextUnformatted($"Our current job is ({localPlayer.ClassJob.RowId}) \"{localPlayer.ClassJob.Value.Abbreviation}\"");

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
	}
}