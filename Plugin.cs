// ====== usingディレクティブ ======
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Numerics;
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
using NAudio.Wave;
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
	private SubWindow SubWindow { get; init; }
	private ConfigWindow ConfigWindow { get; init; }

	private readonly bool[] _keyPressStates = new bool[(int)VirtualKey.F12 + 1];

	private WaveOutEvent? _waveOut;
	private readonly string _soundsDir;
	private readonly Dictionary<string, byte[]> _soundCache = new();

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

		_soundsDir = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Sounds");
		PreloadAllSounds();
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
		_soundCache.Clear();
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
		HandleKeyPressEvent(VirtualKey.F7, () => { /* F7 */ });
		HandleKeyPressEvent(VirtualKey.F9, () => { /* F9 */ });
		HandleKeyPressEvent(VirtualKey.F11, () => { /* F11 */ });

		float delta = (float)Framework.UpdateDelta.TotalSeconds;
		UpdateTextDisplayTimer(ref isF1TextActive, ref showF1Text, ref f1Timer, f1Duration, delta);
		UpdateTextDisplayTimer(ref isF3TextActive, ref showF3Text, ref f3Timer, f3Duration, delta);
		UpdateTextDisplayTimer(ref isF4TextActive, ref showF4Text, ref f4Timer, f4Duration, delta);

		// カウントダウン更新
		if (isF5Running)
		{
			f5Remaining -= delta;
			if (f5Remaining <= 0)
			{
				f5Remaining = 0;
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
		PlaySoundByName("warning.wav");
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
		PlaySoundByName("recall.wav");
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
		PlaySoundByName("alert.wav");
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
		isF5Running = true;
		showF5Timer = true;
		f5Remaining = 60f;
		PlaySoundByName("timer.wav");

		ChatGui.Print(new XivChatEntry
		{
			Message = new SeStringBuilder().AddText("[ScouterX] カウントダウン開始 (60秒)").Build(),
			Type = XivChatType.Debug
		});
	}

	private void PreloadAllSounds()
	{
		try
		{
			var assembly = Assembly.GetExecutingAssembly();
			var resources = assembly.GetManifestResourceNames()
				.Where(n => n.EndsWith(".wav", StringComparison.OrdinalIgnoreCase));

			foreach (var resName in resources)
			{
				using var stream = assembly.GetManifestResourceStream(resName);
				if (stream == null)
				{
					Log.Warning($"Resource stream not found: {resName}");
					continue;
				}
				using var mem = new MemoryStream();
				stream.CopyTo(mem);
				string key = Path.GetFileName(resName);
				_soundCache[key] = mem.ToArray();
			}
			Log.Information($"Embedded {_soundCache.Count} sound(s) preloaded from resources.");
		}
		catch (Exception ex)
		{
			Log.Error($"Error preloading embedded sounds: {ex.Message}");
		}
	}

	// ====== Sound ======
	private MemoryStream? _activeStream;
	private void PlaySoundByName(string fileName)
	{
		try
		{
			var assembly = Assembly.GetExecutingAssembly();

			string? resourceName = assembly.GetManifestResourceNames()
				.FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

			if (resourceName == null)
			{
				Log.Warning($"Embedded sound not found: {fileName}");
				return;
			}

			var stream = assembly.GetManifestResourceStream(resourceName);
			if (stream == null)
			{
				Log.Warning($"Resource stream not found for {resourceName}");
				return;
			}

			_waveOut?.Stop();
			_waveOut?.Dispose();
			_activeStream?.Dispose();

			var memStream = new MemoryStream();
			stream.CopyTo(memStream);
			memStream.Position = 0;
			_activeStream = memStream;

			var reader = new WaveFileReader(memStream);
			_waveOut = new WaveOutEvent();
			_waveOut.Init(reader);
			_waveOut.Play();

			_waveOut.PlaybackStopped += (_, _) =>
			{
				reader.Dispose();
				_waveOut?.Dispose();
				_waveOut = null;
				_activeStream?.Dispose();
				_activeStream = null;
			};

			Log.Information($"Playing embedded sound: {fileName}");
		}
		catch (Exception ex)
		{
			Log.Error($"Error playing embedded sound '{fileName}': {ex.Message}");
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
}