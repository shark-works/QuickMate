using System;
using ImGuiNET;
using System.IO;
using NAudio.Wave;
using Dalamud.IoC;
using System.Text;
using Dalamud.Plugin;
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
	// ======【プラグインサービス】======
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
	private SubWindow SubWindow { get; init; }
	private ConfigWindow ConfigWindow { get; init; }

	private readonly Dictionary<VirtualKey, bool> _keyPressStates = new();

	private WaveOutEvent? waveOut;
	private AudioFileReader? audioFile;
	private readonly string beepPath;

	public bool showF3Text = false;
	public float f3Timer = 0f;
	private readonly float f3Duration = 3.0f;
	private bool isF3TextActive = false;

	public bool showF4Text = false;
	public float f4Timer = 0f;
	private readonly float f4Duration = 2.0f;
	private bool isF4TextActive = false;

	// ======【コンストラクタ】======
	public Plugin()
	{
		Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

		var ImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "icon.png");

		MainWindow = new MainWindow(this, ImagePath);
		SubWindow = new SubWindow(this);
		ConfigWindow = new ConfigWindow(this);

		WindowSystem.AddWindow(MainWindow);
		WindowSystem.AddWindow(SubWindow);
		WindowSystem.AddWindow(ConfigWindow);

		PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
		PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
		PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

		CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
		{
			HelpMessage = "設定ウィンドウ表示"
		});

		Framework.Update += OnFrameworkUpdate;

		Log.Information($"=== {PluginInterface.Manifest.Name} ===");

		InitializeKeyPressStates();

		beepPath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Sounds", "Beep.wav");
		LoadBeepSound();
	}

    // ======【デストラクタ ======
	public void Dispose()
	{
		PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
		PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
		Framework.Update -= OnFrameworkUpdate;

		WindowSystem.RemoveWindow(MainWindow);
		WindowSystem.RemoveWindow(SubWindow);
		WindowSystem.RemoveWindow(ConfigWindow);

		MainWindow.Dispose();
		SubWindow.Dispose();
		ConfigWindow.Dispose();

		PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
		CommandManager.RemoveHandler(CommandName);

		waveOut?.Stop();
		waveOut?.Dispose();
		audioFile?.Dispose();
	}

	private void OnCommand(string command, string args) => MainWindow.Toggle();

	public void ToggleMainUi() => MainWindow.Toggle();
	public void ToggleConfigUi() => ConfigWindow.Toggle();

    private void InitializeKeyPressStates()
    {
        _keyPressStates[VirtualKey.F1] = false;
        _keyPressStates[VirtualKey.F3] = false;
        _keyPressStates[VirtualKey.F4] = false;
        _keyPressStates[VirtualKey.F5] = false;
        _keyPressStates[VirtualKey.F7] = false;
        _keyPressStates[VirtualKey.F9] = false;
        _keyPressStates[VirtualKey.F11] = false;
    }

    private void LoadBeepSound()
    {
        if (File.Exists(beepPath))
        {
            try
            {
                audioFile = new AudioFileReader(beepPath);
                waveOut = new WaveOutEvent();
                waveOut.Init(audioFile);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to load beep sound from {beepPath}: {ex.Message}");
                audioFile?.Dispose();
                waveOut?.Dispose();
                audioFile = null;
                waveOut = null;
            }
        }
        else
        {
            Log.Warning($"Beep.wav not found at {beepPath}");
        }
    }

	// ====== フレーム処理 ======
	private unsafe void OnFrameworkUpdate(IFramework _)
	{
		HandleKeyPressEvent(VirtualKey.F1, () => { /* F1 */ });
		HandleKeyPressEvent(VirtualKey.F3, HandleF3KeyPress);
		HandleKeyPressEvent(VirtualKey.F4, HandleF4KeyPress);
		HandleKeyPressEvent(VirtualKey.F5, () => { /* F5 */ });
		HandleKeyPressEvent(VirtualKey.F7, () => { /* F7 */ });
		HandleKeyPressEvent(VirtualKey.F9, () => { /* F9 */ });
		HandleKeyPressEvent(VirtualKey.F11, () => { /* F11 */ });

		UpdateTextDisplayTimer(ref isF3TextActive, ref showF3Text, ref f3Timer, f3Duration, (float)Framework.UpdateDelta.TotalSeconds);
		UpdateTextDisplayTimer(ref isF4TextActive, ref showF4Text, ref f4Timer, f4Duration, (float)Framework.UpdateDelta.TotalSeconds);
	}

    private void HandleKeyPressEvent(VirtualKey key, Action? onPressed)
    {
        bool nowState = KeyState[key];
        if (!_keyPressStates.ContainsKey(key))
        {
            _keyPressStates[key] = false;
        }

        if (nowState && !_keyPressStates[key])
        {
            onPressed?.Invoke();
        }
        _keyPressStates[key] = nowState;
    }

    private unsafe void HandleF3KeyPress()
	{
		PlayBeepSound();

		isF3TextActive = true;
		f3Timer = f3Duration;
		showF3Text = true;

		var am = ActionManager.Instance();
		if (am == null)
		{
			Log.Warning("ActionManager.Instance() returned null when F3 was pressed.");
			return;
		}

		uint actionId = 29229;
		float realRecast = am->GetRecastTime(ActionType.Action, actionId);
		float realElapsed = am->GetRecastTimeElapsed(ActionType.Action, actionId);
		float remaining = Math.Abs(realRecast - realElapsed);

		bool isMounted = Condition[ConditionFlag.Mounted];
		ushort territoryId = ClientState.TerritoryType;

		string statusIds = GetLocalPlayerStatusIds();

		uint mountId = 71;
		am->UseAction(ActionType.Mount, mountId);

		var seMessage = new SeStringBuilder()
    	.AddText(
        	$"F3: Cooldown({actionId})={remaining:0.00}s | " +
        	$"Mounted={isMounted} | " +
        	$"TerritoryId={territoryId} | " +
        	$"StatusIds=[{statusIds.Trim()}]"
    	)
    	.Build();

		ChatGui.Print(new XivChatEntry
		{
			Message = seMessage,
			Type = XivChatType.Debug
		});
	}

    private void HandleF4KeyPress()
    {
        isF4TextActive = true;
        f4Timer = f4Duration;
        showF4Text = true;

        ChatGui.Print(new XivChatEntry
        {
            Message = new SeStringBuilder()
                .AddText("[QuickMate] F4キーが押されました").Build(),
            Type = XivChatType.Debug
        });
    }

    private void PlayBeepSound()
    {
        if (waveOut != null && audioFile != null)
        {
            try
            {
                waveOut.Stop();
                audioFile.Position = 0;
                waveOut.Init(audioFile);
                waveOut.Play();
            }
            catch (Exception ex)
            {
                Log.Error($"Error playing beep sound: {ex.Message}");
            }
        }
    }

    private string GetLocalPlayerStatusIds()
    {
        var statusStringBuilder = new StringBuilder();
        if (ClientState.LocalPlayer != null)
        {
            var statuses = ClientState.LocalPlayer.StatusList;
            foreach (var s in statuses)
            {
                if (s.StatusId != 0)
                {
                    statusStringBuilder.Append($"{s.StatusId} ");
                }
            }
        }
        return statusStringBuilder.ToString();
    }

    private void UpdateTextDisplayTimer(ref bool isActiveFlag, ref bool showTextFlag, ref float timer, float duration, float deltaTime)
    {
        if (isActiveFlag)
        {
            timer -= deltaTime;
			if (timer <= 0)
			{
				isActiveFlag = false;
				showTextFlag = false;
				timer = 0f;
            }
        }
    }
}