using System;                                 // TimeSpan 用
using System.IO;                              // Path 組み合わせなどに使用
using NAudio.Wave;                            // NAudio WAV 再生用
using Dalamud.IoC;                            // [PluginService] 属性
using Dalamud.Plugin;                         // IDalamudPlugin, DalamudPluginInterface など
using Dalamud.Game.Text;                      // XivChatEntry, XivChatType を使うため
using QuickMate.Windows;                      // ConfigWindow / MainWindow の namespace
using Dalamud.Game.Command;                   // /slash コマンド用（CommandManager, CommandInfo）
using Dalamud.Plugin.Services;                // IClientState, IDataManager, IPluginLog, ITextureProvider, ICommandManager
using Dalamud.Interface.Windowing;            // WindowSystem（UIウィンドウ管理）
using Dalamud.Game.ClientState.Keys;          // VirtualKeyを使う(F3 キー検出用)
using Dalamud.Game.Text.SeStringHandling;     // SeStringBuilder を使うため
using Dalamud.Game.ClientState.Conditions;    // ConditionFlag を使うため
using FFXIVClientStructs.FFXIV.Client.Game;   // ActionManager

namespace QuickMate;

// ===============================
//    【メインプラグインクラス】
// ===============================
public sealed class Plugin : IDalamudPlugin
{
	// ===============================
	// ① Dalamud サービスの注入 == [PluginService] を付けると Dalamud が自動的にインスタンスを設定
	// ・PluginInterface : プラグインの設定やイベントフックを扱う
	// ・TextureProvider : 画像リソースをロードする
	// ・CommandManager  : /コマンド登録・解除を行う
	// ・ClientState     : プレイヤーやワールドの状態取得
	// ・DataManager     : Lumina 経由でゲームデータへアクセス
	// ・Log             : Dalamud の内部ログ出力
	// ・Framework       : 毎フレーム呼ばれる Update イベントを購読できる
	// ・KeyState        : キーボード入力を監視
	// ・ChatGui         : ゲーム内にメッセージを表示（/echo相当）
	// ===============================

	[PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
	[PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
	[PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
	[PluginService] internal static IClientState ClientState { get; private set; } = null!;
	[PluginService] internal static IDataManager DataManager { get; private set; } = null!;
	[PluginService] internal static IPluginLog Log { get; private set; } = null!;
	[PluginService] internal static IFramework Framework { get; private set; } = null!; // キー入力とチャット出力に必要なサービス
	[PluginService] internal static IKeyState KeyState { get; private set; } = null!;   // キー入力とチャット出力に必要なサービス
	[PluginService] internal static IChatGui ChatGui { get; private set; } = null!;     // キー入力とチャット出力に必要なサービス
	[PluginService] internal static ICondition Condition { get; private set; } = null!; // Condition サービス

	// ===============================
	// ② プラグイン固有のフィールド
	// - CommandName : 登録する /pmycommand の名前
	// - Configuration : 設定オブジェクト
	// - WindowSystem : ImGui ウィンドウ管理
	// - ConfigWindow, MainWindow : 設定ウィンドウとメインウィンドウのインスタンス
	// - lastF3 : F3キーが前フレームで押されていたかどうか
	// ===============================

	// コマンド名、UI システム, 登録するスラッシュコマンド
	private const string CommandName = "/pmycommand";

	// 設定オブジェクト
	public Configuration Configuration { get; init; }

	// WindowSystem : IMGUI ベースのウィンドウ
	public readonly WindowSystem WindowSystem = new("QuickMate");

	// ConfigWindow / MainWindow
	private ConfigWindow ConfigWindow { get; init; }
	private MainWindow MainWindow { get; init; }

	// ★追加: F3 の押下状態を記録する変数
	private bool lastF3 = false;

	// NAudio WAV 再生用フィールド
	private WaveOutEvent? waveOut;
	private AudioFileReader? audioFile;
	private readonly string beepPath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Sounds", "Beep.wav");

	// ===============================
	// ③ コンストラクタ == プラグインロード時に実行される初期化処理
	// - 設定をロード
	// - ウィンドウを作成して WindowSystem に登録
	// - スラッシュコマンド登録
	// - UiBuilder の描画イベント、UI トグルイベント登録
	// - Framework.Update で毎フレーム呼ばれる OnFrameworkUpdate を登録
	// - ロード確認ログを出力
	// ===============================

	public Plugin()
	{
		// 設定をロード（保存が無ければ新規作成）
		Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

		// リソースのパス例
		var ImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "icon_shark.png");

		// ウィンドウ初期化, ウィンドウインスタンス作成
		ConfigWindow = new ConfigWindow(this);
		MainWindow = new MainWindow(this, ImagePath);

		// WindowSystem に登録
		WindowSystem.AddWindow(ConfigWindow);
		WindowSystem.AddWindow(MainWindow);

		// コマンド登録
		CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
		{
			HelpMessage = "メインウィンドウを表示/非表示"
		});

		// UiBuilder に Draw イベント登録
		PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

		// UI トグルイベント登録
		PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
		PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

		// Framework.Update にイベント登録
		Framework.Update += OnFrameworkUpdate;

		// ログ出力：プラグインロード確認
		Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");

		// NAudio WAV 再生の初期化
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

	// ===============================
	// ④ Dispose == プラグインがアンロードされるときに呼ばれる後始末
	// - 登録したイベントを解除
	// - ウィンドウを破棄
	// - コマンド解除
	// ===============================
	public void Dispose()
	{
		// UiBuilder イベント解除
		PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
		PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
		PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

		// Update イベント解除
		Framework.Update -= OnFrameworkUpdate;

		// ウィンドウ破棄
		WindowSystem.RemoveAllWindows();
		ConfigWindow.Dispose();
		MainWindow.Dispose();

		// 登録したコマンドを解除
		CommandManager.RemoveHandler("/pmycommand");

		// NAudio WAV 後始末
		waveOut?.Stop();
		waveOut?.Dispose();
		audioFile?.Dispose();
	}

	// ===============================
	// ⑤ コマンドハンドラ
	// /pmycommand を実行したとき MainWindow の表示状態を切り替える
	// ===============================
	private void OnCommand(string command, string args)
	{
		MainWindow.Toggle();
	}

	// ===============================
	// ⑥ UIトグルメソッド
	// UI トグル用ラッパー
	// UiBuilder.OpenConfigUi / OpenMainUi イベントから呼ばれる
	// ===============================
	public void ToggleConfigUi() => ConfigWindow.Toggle();
	public void ToggleMainUi() => MainWindow.Toggle();

	// ===============================
	// ⑦ 毎フレーム更新処理
	//  KeyState[VirtualKey.F3] で F3 キーの押下状態を取得。
	//・押された瞬間だけ ChatGui.Print で /echo と同じ挙動を表示。
	//・押しっぱなしでは連続出力されないよう lastF3 で前回状態を保持。
	// ===============================
	private unsafe void OnFrameworkUpdate(IFramework _)
	{
		bool now = KeyState[VirtualKey.F3];
		if (now && !lastF3)
		{
			// NAudio WAV を再生（1回だけ）
    		if (waveOut != null && audioFile != null)
    		{
        		audioFile.Position = 0; // 先頭から再生
        		waveOut.Play();
    		}

			var am = ActionManager.Instance();
			if (am != null)
			{
				// クールダウンを計算
				uint actionId = 29229;
				float realRecast = am->GetRecastTime(ActionType.Action, actionId);
				float realElapsed = am->GetRecastTimeElapsed(ActionType.Action, actionId);
				float remaining = Math.Abs(realRecast - realElapsed);

				// マウント判定 (ConditionFlag.Mounted)
				bool isMounted = Condition[ConditionFlag.Mounted];

				// ClientState から現在のエリアID (TerritoryTypeId) を取得
				ushort territoryId = ClientState.TerritoryType;

				// バフ・デバフID一覧
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
				// マウントアクション実行
				uint mountId = 71;
				am->UseAction(ActionType.Mount, mountId);

				// メッセージ出力 (Debugモード明示)
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
	}
}