// ====== usingディレクティブ ======
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Game.Text;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.ClientState.Conditions;
using ImGuiNET;
using ScouterX.Windows;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace ScouterX;

public sealed class Plugin : IDalamudPlugin
{

	// ====== サービスインジェクション ======
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


	// ====== フィールド (データ保持) ======
	public Configuration Configuration { get; init; }
	private const string CommandName = "/qm";
	public readonly WindowSystem WindowSystem = new("ScouterX");

	private MainWindow MainWindow { get; init; }
	private DrawManager DrawManager { get; init; }
	private ConfigWindow ConfigWindow { get; init; }

	private AudioManager _audioManager;

	private readonly bool[] _keyPressStates = new bool[(int)VirtualKey.F12 + 1];

	public bool showF1Text = false;
	public float f1Timer = 0f;
	private readonly float f1Duration = 3.0f;
	private bool isF1TextActive = false;

	public bool showF3Text = false;
	public float f3Timer = 0f;
	private readonly float f3Duration = 3.0f;
	private bool isF3TextActive = false;

	public bool showF4Text = false;
	public float f4Timer = 0f;
	private readonly float f4Duration = 3.0f;
	private bool isF4TextActive = false;

	public bool showF5Timer = false;
	public float f5Remaining = 0f;
	private bool isF5Running = false;


	// ====== コンストラクタ ======
	public Plugin()
	{
		Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

		string imagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "icon.png");

		MainWindow = new MainWindow(this, imagePath);
		DrawManager = new DrawManager(this);
		ConfigWindow = new ConfigWindow(this);

		WindowSystem.AddWindow(MainWindow);
		WindowSystem.AddWindow(DrawManager);
		WindowSystem.AddWindow(ConfigWindow);

		PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
		PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
		PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

		CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
		{
			HelpMessage = "設定ウィンドウ表示"
		});

		Framework.Update += OnFrameworkUpdate;

		ClientState.Login += OnLogin;
		ClientState.Logout += OnLogout;

		Log.Information($"=== {PluginInterface.Manifest.Name} ===");

		_audioManager = new AudioManager(Log);
	}


	// ====== デストラクタ ======
	public void Dispose()
	{
		PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
		PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
		Framework.Update -= OnFrameworkUpdate;
		PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;

		ClientState.Login -= OnLogin;
    	ClientState.Logout -= OnLogout;

		CommandManager.RemoveHandler(CommandName);

		WindowSystem.RemoveWindow(MainWindow);
		WindowSystem.RemoveWindow(DrawManager);
		WindowSystem.RemoveWindow(ConfigWindow);

		MainWindow.Dispose();
		DrawManager.Dispose();
		ConfigWindow.Dispose();

		_audioManager.Dispose();
	}


	// ====== プライベートメソッド (データ処理) ======
	private void OnCommand(string command, string args) => MainWindow.Toggle();
	public void ToggleMainUi() => MainWindow.Toggle();
	public void ToggleConfigUi() => ConfigWindow.Toggle();

	private unsafe void OnFrameworkUpdate(IFramework _)
	{
		HandleKeyPressEvent(VirtualKey.F1, HandleF1KeyPress);
		HandleKeyPressEvent(VirtualKey.F3, HandleF3KeyPress);
		HandleKeyPressEvent(VirtualKey.F4, HandleF4KeyPress);
		HandleKeyPressEvent(VirtualKey.F5, HandleF5KeyPress);
		//HandleKeyPressEvent(VirtualKey.F7, () => { /* F7 */ });
		//HandleKeyPressEvent(VirtualKey.F9, () => { /* F9 */ });
		//HandleKeyPressEvent(VirtualKey.F11, () => { /* F11 */ });

		float delta = (float)Framework.UpdateDelta.TotalSeconds;
		UpdateTextDisplayTimer(ref isF1TextActive, ref showF1Text, ref f1Timer, f1Duration, delta);
		UpdateTextDisplayTimer(ref isF3TextActive, ref showF3Text, ref f3Timer, f3Duration, delta);
		UpdateTextDisplayTimer(ref isF4TextActive, ref showF4Text, ref f4Timer, f4Duration, delta);

		// カウントダウンタイマー更新
		if (isF5Running)
		{
    		f5Remaining -= delta;
    		if (f5Remaining < 0f)
        		f5Remaining = 0f;

    		if (f5Remaining == 0f)
    		{
        		isF5Running = false;
        		showF5Timer = false;
    		}
		}
	}

	private void HandleKeyPressEvent(VirtualKey key, Action? onPressed)
	{
		int keyIndex = (int)key;
		if (keyIndex >= _keyPressStates.Length || keyIndex < 0)
		{
			Log.Warning($"Invalid VirtualKey index: {keyIndex} for key {key}. Skipping key press handling.");
			return;
		}

		bool nowState = KeyState[key];

		if (nowState && !_keyPressStates[keyIndex])
			onPressed?.Invoke();

		_keyPressStates[keyIndex] = nowState;
	}

	// ====== F1 ======
	private void HandleF1KeyPress()
	{
		_audioManager.PlaySoundByName("warning.wav");
		isF1TextActive = true;
		f1Timer = f1Duration;
		showF1Text = true;
		ChatGui.Print(new XivChatEntry
		{
			Message = new SeStringBuilder()
				.AddText("[ScouterX] F1キーが押されました").Build(),
			Type = XivChatType.Debug
		});
	}

	// ====== F3 ======
	private unsafe void HandleF3KeyPress()
	{
		_audioManager.PlaySoundByName("recall.wav");
		isF3TextActive = true;
		f3Timer = f3Duration;
		showF3Text = true;

		var am = ActionManager.Instance();
		if (am == null)
		{
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
			).Build();
		ChatGui.Print(new XivChatEntry
		{
			Message = seMessage,
			Type = XivChatType.Debug
		});
	}

	// ====== F4 ======
	private void HandleF4KeyPress()
	{
		_audioManager.PlaySoundByName("alert.wav");
		isF4TextActive = true;
		f4Timer = f4Duration;
		showF4Text = true;
		ChatGui.Print(new XivChatEntry
		{
			Message = new SeStringBuilder()
				.AddText("[ScouterX] F4キーが押されました").Build(),
			Type = XivChatType.Debug
		});
	}

	// ====== F5 ======
	private void HandleF5KeyPress()
	{
		_audioManager.PlaySoundByName("caution.wav");
		isF5Running = true;
		showF5Timer = true;
		f5Remaining = 60f;

		ChatGui.Print(new XivChatEntry
		{
			Message = new SeStringBuilder().AddText("[ScouterX] カウントダウン開始 (60秒)").Build(),
			Type = XivChatType.Debug
		});
	}

	private void WarmupAudio()
	{
		try
		{
			Log.Information("Warming up audio manager with Null.wav...");
			_audioManager.PlaySoundByName("Null.wav");

			// 少し待ってから、確実に停止させます。
			// PlaySoundByNameがTask.Runで実行されるため、SleepがないとPlaySoundByNameの実行前にStopAllSoundsが呼ばれる可能性がある
			Thread.Sleep(200); // 200ms程度待機

			// ウォームアップ用の無音再生をすぐに停止します
			_audioManager.StopAllSounds(); // AudioManagerにStopAllSounds()を追加した場合

			Log.Information("Audio manager warmed up.");
		}
		catch (Exception ex)
		{
			Log.Error($"Error warming up audio manager: {ex.Message}");
		}
	}


	// ====== GetStatus ======
	private string GetLocalPlayerStatusIds()
	{
		if (ClientState.LocalPlayer == null)
		{
			return string.Empty;
		}
		return string.Join(" ", ClientState.LocalPlayer.StatusList
			.Where(s => s.StatusId != 0)
			.Select(s => s.StatusId.ToString()));
	}

	// ====== Timer ======
	private void UpdateTextDisplayTimer(ref bool isActive, ref bool showText, ref float timer, float duration, float deltaTime)
	{
		if (!isActive)
			return;
		timer -= deltaTime;
		if (timer <= 0)
		{
			isActive = false;
			showText = false;
			timer = 0f;
		}
	}

	private void OnLogin()
	{
		if (!Configuration.OpenOnLogin) return;
    	MainWindow.IsOpen = false;
    	DrawManager.IsOpen = true;
    	Log.Information("[ScouterX] Player logged in. Windows opened.");
	}

	private void OnLogout(int type, int code)
	{
    	MainWindow.IsOpen = false;
    	DrawManager.IsOpen = false;
    	Log.Information("[ScouterX] Player logged out. Windows closed.");
	}
}