using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;

namespace QuickMate;

public class QuickMateWarningOverlay : Window, IDisposable
{
    private readonly Plugin plugin;

	public QuickMateWarningOverlay(Plugin plugin)
		: base("QuickMate Warning Overlay##UniqueId",
			  Dalamud.Bindings.ImGui.ImGuiWindowFlags.NoDecoration |
			  Dalamud.Bindings.ImGui.ImGuiWindowFlags.NoBackground |
			  Dalamud.Bindings.ImGui.ImGuiWindowFlags.NoInputs |
			  Dalamud.Bindings.ImGui.ImGuiWindowFlags.NoNav |
			  Dalamud.Bindings.ImGui.ImGuiWindowFlags.NoSavedSettings |
			  Dalamud.Bindings.ImGui.ImGuiWindowFlags.NoFocusOnAppearing |
			  Dalamud.Bindings.ImGui.ImGuiWindowFlags.NoDocking)
	{
		this.plugin = plugin;
		Position = new Vector2(900, 450);
		Size = new Vector2(500, 200);
		SizeCondition = ImGuiCond.Always;
		PositionCondition = ImGuiCond.Always;

		RespectCloseHotkey = false;

		IsOpen = true;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (!plugin.showWarningText)
        {
            return;
        }

        if (Plugin.ClientState == null || Plugin.GameGui == null)
        {
            Plugin.Log.Warning("QuickMateWarningOverlay: ClientState or GameGui is null.");
            return;
        }

        var player = Plugin.ClientState.LocalPlayer;
        if (player == null)
        {
            Plugin.Log.Warning("QuickMateWarningOverlay: LocalPlayer is null.");
            return;
        }

        Vector3 worldPos = player.Position;
        worldPos.Y += 1.8f;

        Vector2 screenPos;
        if (!Plugin.GameGui.WorldToScreen(worldPos, out screenPos))
        {
            Plugin.Log.Warning("QuickMateWarningOverlay: WorldToScreen failed for player position.");
            return;
        }

        Plugin.Log.Debug($"QuickMateWarningOverlay: Calculated screenPos ({screenPos.X:F0}, {screenPos.Y:F0})");

        var text = "Warning";
        var textSize = Dalamud.Bindings.ImGui.ImGui.CalcTextSize(text);

        float padding = 5f;
        var windowSize = new Vector2(textSize.X + padding * 2, textSize.Y + padding * 2);
        var windowPos = new Vector2(screenPos.X - (windowSize.X / 2), screenPos.Y - (windowSize.Y / 2));

        Dalamud.Bindings.ImGui.ImGui.SetNextWindowPos(windowPos);
        Dalamud.Bindings.ImGui.ImGui.SetNextWindowSize(windowSize);

        Plugin.Log.Debug($"QuickMateWarningOverlay: Text='{text}', Calculated Size={textSize}, " +
                         $"WindowPos=({windowPos.X:F0}, {windowPos.Y:F0}), WindowSize=({windowSize.X:F0}, {windowSize.Y:F0})");

        Dalamud.Bindings.ImGui.ImGui.SetCursorPos(Vector2.Zero);

        var textOffsetWithinWindow = new Vector2((windowSize.X - textSize.X) / 2, (windowSize.Y - textSize.Y) / 2);
        Dalamud.Bindings.ImGui.ImGui.SetCursorPos(textOffsetWithinWindow);

        Dalamud.Bindings.ImGui.ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
        Dalamud.Bindings.ImGui.ImGui.TextUnformatted(text);
        Dalamud.Bindings.ImGui.ImGui.PopStyleColor();

        Dalamud.Bindings.ImGui.ImGui.SetCursorPos(Vector2.Zero);
    }
}