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
	private FrameworkManager _frameworkManager;
	private KeyWatcher _keyWatcher;
    private StatusWatcher _statusWatcher; // ★新しいStatusWatcherを追加★

	// KeyWatcherとFrameworkManagerが利用する状態をpublicにするか、プロパティで公開
	public bool showF1Text = false;
	public float f1Timer = 0f;
	public readonly float f1Duration = 3.0f;
	public bool isF1TextActive = false;

	public bool showF3Text = false;
	public float f3Timer = 0f;
	public readonly float f3Duration = 3.0f;
	public bool isF3TextActive = false;

	public bool showF4Text = false;
	public float f4Timer = 0f;
	public readonly float f4Duration = 3.0f;
	public bool isF4TextActive = false;

	public bool showF5Timer = false;
	public float f5Remaining = 0f;
	public bool isF5Running = false;


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

		// AudioManagerを先に初期化
		_audioManager = new AudioManager(Log);

        // StatusWatcherの初期化 (KeyWatcherより前に行う)
        _statusWatcher = new StatusWatcher(ClientState, Log); // ★StatusWatcherを初期化★

		// FrameworkManagerとKeyWatcherの初期化
		_frameworkManager = new FrameworkManager(Framework, this);
		_keyWatcher = new KeyWatcher(KeyState, Log, ChatGui, ClientState, Condition, _audioManager, this, _statusWatcher); // ★StatusWatcherをKeyWatcherに渡す★

		// FrameworkManagerのUpdateイベントにKeyWatcherの処理を結合
		Framework.Update += (f) => _keyWatcher.HandleKeys();


		ClientState.Login += OnLogin;
		ClientState.Logout += OnLogout;

		Log.Information($"=== {PluginInterface.Manifest.Name} ===");

		WarmupAudio();
	}


	// ====== デストラクタ ======
	public void Dispose()
	{
		PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
		PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
		PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;

		// Framework.UpdateイベントからKeyWatcherの処理を分離
		Framework.Update -= (f) => _keyWatcher.HandleKeys();

		// FrameworkManager, KeyWatcher, StatusWatcherをDispose
		_frameworkManager.Dispose();
		_keyWatcher.Dispose();
        _statusWatcher.Dispose(); // ★StatusWatcherをDispose★

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
	public void ToggleMainUi() => MainWindow.Toggle();   // public に修正
	public void ToggleConfigUi() => ConfigWindow.Toggle(); // public に修正

	private void WarmupAudio()
	{
		try
		{
			Log.Information("Warming up audio manager with Null.wav...");
			_audioManager.PlaySoundByName("Null.wav");

			Thread.Sleep(200);

			_audioManager.StopAllSounds();

			Log.Information("Audio manager warmed up.");
		}
		catch (Exception ex)
		{
			Log.Error($"Error warming up audio manager: {ex.Message}");
		}
	}

	// ====== Timer (FrameworkManagerから呼び出される) ======
	public void UpdateTextDisplayTimer(ref bool isActive, ref bool showText, ref float timer, float duration, float deltaTime)
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