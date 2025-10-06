using System;
using ImGuiNET;
using System.IO;
using NAudio.Wave;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using System.Collections.Generic;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Utility;

namespace QuickMate;

public sealed class Plugin : IDalamudPlugin
{
	[PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
	[PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
	[PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
	[PluginService] internal static IClientState ClientState { get; private set; } = null!;
	[PluginService] internal static IDataManager DataManager { get; private set; } = null!;
	[PluginService] internal static ICondition Condition { get; private set; } = null!;
	[PluginService] internal static IFramework Framework { get; private set; } = null!;
	[PluginService] internal static IKeyState KeyState { get; private set; } = null!;
	[PluginService] internal static IGameGui GameGui { get; private set; } = null!;
	[PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
	[PluginService] internal static IPluginLog Log { get; private set; } = null!;

	private const string CommandName = "/pmycommand";

	public Configuration Configuration { get; init; }

	public readonly WindowSystem WindowSystem = new("QuickMate");

	private ConfigWindow ConfigWindow { get; init; }
	private MainWindow MainWindow { get; init; }

	private bool lastF3 = false;

	private WaveOutEvent? waveOut;
	private AudioFileReader? audioFile;
	private readonly string beepPath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Sounds", "Beep.wav");

    public bool showHelloText = false;
    public float helloTimer = 0f;
    private readonly float helloDuration = 2.5f;

	public Plugin()
	{
		Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

		var ImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "icon_shark.png");

		ConfigWindow = new ConfigWindow(this);
		MainWindow = new MainWindow(this, ImagePath);

		WindowSystem.AddWindow(ConfigWindow);
		WindowSystem.AddWindow(MainWindow);

		CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
		{
			HelpMessage = "メインウィンドウを表示/非表示"
		});

		PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
		PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
		PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

		Framework.Update += OnFrameworkUpdate;

		Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");

		if (File.Exists(beepPath))
		{
			audioFile = new AudioFileReader(beepPath);
			waveOut = new WaveOutEvent();
			waveOut.Init(audioFile);
		}
		else
		{
			Log.Warning($"Beep.wav not found at {beepPath}");
		}
	}

	public void Dispose()
	{
		PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
		PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
		PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
		Framework.Update -= OnFrameworkUpdate;

		WindowSystem.RemoveAllWindows();
		ConfigWindow.Dispose();
		MainWindow.Dispose();
		CommandManager.RemoveHandler("/pmycommand");

		waveOut?.Stop();
		waveOut?.Dispose();
		audioFile?.Dispose();
	}

	private void OnCommand(string command, string args)
	{
		MainWindow.Toggle();
	}

	public void ToggleConfigUi() => ConfigWindow.Toggle();
	public void ToggleMainUi() => MainWindow.Toggle();

	private unsafe void OnFrameworkUpdate(IFramework _)
	{
		bool now = KeyState[VirtualKey.F3];
		if (now && !lastF3)
		{
			if (waveOut != null && audioFile != null)
			{
				audioFile.Position = 0;
				waveOut.Play();
			}

			showHelloText = true;
			helloTimer = helloDuration;

			var am = ActionManager.Instance();
			if (am != null)
			{
				uint actionId = 29229;
				float realRecast = am->GetRecastTime(ActionType.Action, actionId);
				float realElapsed = am->GetRecastTimeElapsed(ActionType.Action, actionId);
				float remaining = Math.Abs(realRecast - realElapsed);

				bool isMounted = Condition[ConditionFlag.Mounted];
				ushort territoryId = ClientState.TerritoryType;

				string statusIds = "";
				if (ClientState.LocalPlayer != null)
				{
					var statuses = ClientState.LocalPlayer.StatusList;
					foreach (var s in statuses)
					{
						if (s.StatusId != 0)
						{
							statusIds += $"{s.StatusId} ";
						}
					}
				}
				uint mountId = 71;
				am->UseAction(ActionType.Mount, mountId);

				var seMessage = new SeStringBuilder()
					  .AddText($"F3: Cooldown({actionId})={remaining:0.00}s | Mounted={isMounted} | TerritoryId={territoryId} | StatusIds=[{statusIds.Trim()}]")
					.Build();

				ChatGui.Print(new XivChatEntry
				{
					Message = seMessage,
					Type = XivChatType.Debug
				});
			}
		}
		lastF3 = now;

		if (showHelloText)
		{
			helloTimer -= (float)Framework.UpdateDelta.TotalSeconds;
			if (helloTimer <= 0)
			{
				showHelloText = false;
			}
		}
	}
}