// ====== usingディレクティブ ======
//異なる名前空間に定義されているクラスを使用する
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ImGuiNET;
using NAudio.Wave;
using FFXIVClientStructs.FFXIV.Client.Game;
using QuickMate.Windows;

// 名前空間とクラス
namespace QuickMate;

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

	// ====== フィールド (クラスの状態とデータ保持) ======
	// 「部屋数」「配管場所」といった「家の設計図の構成要素や状態という(データを持っている)」
	public Configuration Configuration { get; init; }

	private const string CommandName = "/qm";

	public readonly WindowSystem WindowSystem = new("QuickMate");
	private MainWindow MainWindow { get; init; }
	private SubWindow SubWindow { get; init; }
	private ConfigWindow ConfigWindow { get; init; }

	private readonly bool[] _keyPressStates = new bool[(int)VirtualKey.F12 + 1];

	private WaveOutEvent? _waveOut;
	private AudioFileReader? _audioFile;
	private readonly string _beepPath;

	public bool showF3Text = false;
	public float f3Timer = 0f;
	private readonly float f3Duration = 3.0f;
	private bool isF3TextActive = false;

	public bool showF4Text = false;
	public float f4Timer = 0f;
	private readonly float f4Duration = 3.0f;
	private bool isF4TextActive = false;

	// ====== コンストラクタ ======
	public Plugin()
	{
		Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

		string imagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "icon.png");

		MainWindow = new MainWindow(this, imagePath);
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

		_beepPath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Sounds", "Beep.wav");
		LoadBeepSound();
	}

	// ====== デストラクタ ======
	public void Dispose()
	{
		PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
		PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
		Framework.Update -= OnFrameworkUpdate;
		PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;

		CommandManager.RemoveHandler(CommandName);

		WindowSystem.RemoveWindow(MainWindow);
		WindowSystem.RemoveWindow(SubWindow);
		WindowSystem.RemoveWindow(ConfigWindow);

		MainWindow.Dispose();
		SubWindow.Dispose();
		ConfigWindow.Dispose();

		_waveOut?.Stop();
		_waveOut?.Dispose();
		_audioFile?.Dispose();
	}

	// ====== プライベートメソッド (クラスの機能と動作) ======
	//「調理」「掃除」といった「家の中で行われる具体的な行動や作業を担う」
	private void OnCommand(string command, string args) => MainWindow.Toggle();
	public void ToggleMainUi() => MainWindow.Toggle();
	public void ToggleConfigUi() => ConfigWindow.Toggle();

	private unsafe void OnFrameworkUpdate(IFramework _)
	{
		HandleKeyPressEvent(VirtualKey.F1, () => { /* F1 */ });
		HandleKeyPressEvent(VirtualKey.F3, HandleF3KeyPress);
		HandleKeyPressEvent(VirtualKey.F4, HandleF4KeyPress);
		HandleKeyPressEvent(VirtualKey.F5, () => { /* F5 */ });
		HandleKeyPressEvent(VirtualKey.F7, () => { /* F7 */ });
		HandleKeyPressEvent(VirtualKey.F9, () => { /* F9 */ });
		HandleKeyPressEvent(VirtualKey.F11, () => { /* F11 */ });

		float delta = (float)Framework.UpdateDelta.TotalSeconds;
		UpdateTextDisplayTimer(ref isF3TextActive, ref showF3Text, ref f3Timer, f3Duration, delta);
		UpdateTextDisplayTimer(ref isF4TextActive, ref showF4Text, ref f4Timer, f4Duration, delta);
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

	private unsafe void HandleF3KeyPress()
	{
		PlayBeepSound();

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

	private void LoadBeepSound()
	{
		if (!File.Exists(_beepPath))
		{
			return;
		}
		try
		{
			_audioFile = new AudioFileReader(_beepPath);
			_waveOut = new WaveOutEvent();
			_waveOut.Init(_audioFile);
		}
		catch (Exception ex)
		{
			Log.Error($"Failed to load beep sound from {_beepPath}: {ex.Message}");
			_audioFile?.Dispose();
			_waveOut?.Dispose();
			_audioFile = null;
			_waveOut = null;
		}
	}

	private void PlayBeepSound()
	{
		if (_waveOut == null || _audioFile == null)
		{
			return;
		}
		try
		{
			_waveOut.Stop();           // 再生中は停止
			_audioFile.Position = 0;   // 再生位置を先頭
			_waveOut.Init(_audioFile); // 再初期化
			_waveOut.Play();           // 再生
		}
		catch (Exception ex)
		{
			Log.Error($"Error playing beep sound: {ex.Message}");
		}
	}

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
}