using System;
using ImGuiNET;
using System.IO;
using NAudio.Wave;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.Numerics;
using Dalamud.Game.Text;
using QuickMate.Windows;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Utility;
using System.Collections.Generic;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Game.ClientState.Objects.Types;

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

	// ======【フィールドとメンバー】======
	public Configuration Configuration { get; init; }
	private const string CommandName = "/qm";

	public readonly WindowSystem WindowSystem = new("QuickMate");
	private MainWindow MainWindow { get; init; }
	private SubWindow InfoOverlay { get; init; }
	private ConfigWindow ConfigWindow { get; init; }

	private bool lastF3 = false;
	private bool lastF4 = false;

	private WaveOutEvent? waveOut;
	private AudioFileReader? audioFile;
	private readonly string beepPath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Sounds", "Beep.wav");

	// ====== F3 / F4 表示制御 ======
	public bool showF3Text = false;
	public float f3Timer = 0f;
	private readonly float f3Duration = 3.0f;
	private bool isF3TextActive = false;

	public bool showF4Text = false;
	public float f4Timer = 0f;
	private readonly float f4Duration = 3.0f;
	private bool isF4TextActive = false;

	// ======【コンストラクタ】======
	public Plugin()
	{
		Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

		var ImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "icon.png");

		MainWindow = new MainWindow(this, ImagePath);
		WindowSystem.AddWindow(MainWindow);

		ConfigWindow = new ConfigWindow(this);
		WindowSystem.AddWindow(ConfigWindow);

		InfoOverlay = new SubWindow(this);
		WindowSystem.AddWindow(InfoOverlay);

		PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

		CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
		{
			HelpMessage = "設定ウィンドウ表示"
		});

		PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
		PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

		Framework.Update += OnFrameworkUpdate;

		Log.Information($"=== {PluginInterface.Manifest.Name} ===");

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

	// ======【デストラクタ】======
	public void Dispose()
	{
		PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
		PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;

		Framework.Update -= OnFrameworkUpdate;

		WindowSystem.RemoveWindow(MainWindow);
		MainWindow.Dispose();

		WindowSystem.RemoveWindow(ConfigWindow);
		ConfigWindow.Dispose();

		WindowSystem.RemoveWindow(InfoOverlay);
		InfoOverlay.Dispose();

		PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;

		CommandManager.RemoveHandler("/qm");

		waveOut?.Stop();
		waveOut?.Dispose();
		audioFile?.Dispose();
	}

	// ====== コマンドハンドラ ======
	private void OnCommand(string command, string args)
	{
		MainWindow.Toggle();
	}

	// ====== UIトグルメソッド ======
	public void ToggleMainUi() => MainWindow.Toggle();
	public void ToggleConfigUi() => ConfigWindow.Toggle();

	// ====== フレーム処理 ======
	private unsafe void OnFrameworkUpdate(IFramework _)
	{
		bool now = KeyState[VirtualKey.F3];
		if (now && !lastF3)
		{
			if (waveOut != null && audioFile != null)
			{
    			waveOut.Stop();
    			audioFile.Position = 0;
    			waveOut.Init(audioFile);
    			waveOut.Play();
			}

			isF3TextActive = true;
			f3Timer = f3Duration;
			showF3Text = true;

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

		bool nowF4 = KeyState[VirtualKey.F4];
    	if (nowF4 && !lastF4)
    	{
        	isF4TextActive = true;
        	f4Timer = f4Duration;
        	showF4Text = true;

        	ChatGui.Print(new Dalamud.Game.Text.XivChatEntry
        	{
            	Message = new Dalamud.Game.Text.SeStringHandling.SeStringBuilder()
                	.AddText("[QuickMate] F4キーが押されました").Build(),
            	Type = XivChatType.Debug
        	});
    	}
    	lastF4 = nowF4;

		// ====== タイマー管理 ======
		if (isF3TextActive)
		{
			f3Timer -= (float)Framework.UpdateDelta.TotalSeconds;
			if (f3Timer <= 0)
				isF3TextActive = showF3Text = false;
		}

    	if (isF4TextActive)
    	{
        	f4Timer -= (float)Framework.UpdateDelta.TotalSeconds;
        	if (f4Timer <= 0)
            	isF4TextActive = showF4Text = false;
    	}
	}
}