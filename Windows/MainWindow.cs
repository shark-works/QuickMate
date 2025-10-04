using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace QuickMate.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly string ImagePath; // 表示する画像のパス
    private readonly Plugin plugin;    // Plugin インスタンス保持

	//【コンストラクタ：ウィンドウ初期化】
	// このウィンドウには、## を使用して隠し ID を付与
	// ウィンドウタイトル「My Amazing Window」
	// しかし、ImGuiの場合、IDは「My Amazing Window##With a hidden ID」
	public MainWindow(Plugin plugin, string ImagePath)
		: base("My Amazing Window##With a hidden ID", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
	{
		// ウィンドウサイズ制約
		SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(375, 330),
			MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
		};

		this.ImagePath = ImagePath;
		this.plugin = plugin;
	}

	// IDisposable 実装
	public void Dispose() { }

	// Draw(): ウィンドウ描画処理
	public override void Draw()
	{
		// 設定の表示
		ImGui.TextUnformatted($"The random config bool is {plugin.Configuration.SomePropertyToBeSavedAndWithADefault}");

		// 設定ウィンドウ表示ボタン
		if (ImGui.Button("Show Settings"))
		{
			plugin.ToggleConfigUi();
		}

		ImGui.Spacing();

		//【子ウィンドウ（スクロール可能）を作成】
		// 通常、BeginChild() の後には無条件の EndChild() が続く必要あり
		// スコープ終了後はImRaiiがこれを処理
		// これは、特定の処理を必要とするすべての ImGui 関数で機能。例としては、BeginTable() または Indent()
		using (var child = ImRaii.Child("SomeChildWithAScrollbar", Vector2.Zero, true))
		{
			// 画像表示, childが絵を描いているか確認
			if (child.Success)
			{
				ImGui.TextUnformatted("Have a Image:");
				var goatImage = Plugin.TextureProvider.GetFromFile(ImagePath).GetWrapOrDefault();
				if (goatImage != null)
				{
					using (ImRaii.PushIndent(55f))
					{
						ImGui.Image(goatImage.Handle, goatImage.Size);
					}
				}
				else
				{
					ImGui.TextUnformatted("Image not found.");
				}

				ImGuiHelpers.ScaledDummy(20.0f);

            	// ClientState を利用したプレイヤー情報の表示
				// Dalamud が提供するその他のサービスの例
				// ClientState は、ローカル プレーヤー オブジェクトとクライアントに関する情報が入ったラッパーを提供
				var localPlayer = Plugin.ClientState.LocalPlayer;
				if (localPlayer == null)
				{
					ImGui.TextUnformatted("Our local player is currently not loaded.");
					return;
				}

				if (!localPlayer.ClassJob.IsValid)
				{
					ImGui.TextUnformatted("Our current job is currently not valid.");
					return;
				}

				// このSeStringのマクロ表現を見たい場合は、`ToMacroString()`を使用する
				ImGui.TextUnformatted($"Our current job is ({localPlayer.ClassJob.RowId}) \"{localPlayer.ClassJob.Value.Abbreviation}\"");

				// Lumina を使った現在地域情報
				// Lumina を直接取得、現在エリア名を取得する例
				var territoryId = Plugin.ClientState.TerritoryType;
				if (Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territoryRow))
				{
					ImGui.TextUnformatted($"We are currently in ({territoryId}) \"{territoryRow.PlaceName.Value.Name}\"");
				}
				else
				{
					ImGui.TextUnformatted("Invalid territory.");
				}
			}
		}
	}
}
