﻿using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ScouterX.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin)
        : base("Configuration###ScouterXConfig")
    {
        Flags = ImGuiWindowFlags.NoResize
              | ImGuiWindowFlags.NoCollapse
              | ImGuiWindowFlags.NoScrollbar
              | ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(232, 90);
        SizeCondition = ImGuiCond.Always;

		configuration = plugin.Configuration;
	}

	public void Dispose() { }

	public override void PreDraw()
	{
		if (configuration.IsConfigWindowMovable)
		{
			Flags &= ~ImGuiWindowFlags.NoMove;
		}
		else
		{
			Flags |= ImGuiWindowFlags.NoMove;
		}
	}

	public override void Draw()
	{
		var configValue = configuration.SomePropertyToBeSavedAndWithADefault;
		if (ImGui.Checkbox("Random Config Bool", ref configValue))
		{
			configuration.SomePropertyToBeSavedAndWithADefault = configValue;
			configuration.Save();
		}

		var movable = configuration.IsConfigWindowMovable;
		if (ImGui.Checkbox("Movable Config Window", ref movable))
		{
			configuration.IsConfigWindowMovable = movable;
			configuration.Save();
		}
	}
}
