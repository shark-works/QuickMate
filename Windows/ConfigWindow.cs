using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace QuickMate.Windows;

public class ConfigWindow : Window, IDisposable
{
	// 設定オブジェクト保持
    private readonly Configuration configuration;

	//【コンストラクタ：ウィンドウ初期化】
	// このウィンドウには、### を使用して定数 ID を割り当て
	// これにより、ラベルを「{FPSカウンター}fps###XYZカウンターウィンドウ」のように動的にすることができる
	// ImGuiのウィンドウIDは常に「###XYZカウンターウィンドウ」になる
    public ConfigWindow(Plugin plugin)
        : base("QuickMate Configuration###QuickMateConfig") // ImGui ID に ### を使用
    {
		// ウィンドウフラグ設定
        Flags = ImGuiWindowFlags.NoResize
              | ImGuiWindowFlags.NoCollapse
              | ImGuiWindowFlags.NoScrollbar
              | ImGuiWindowFlags.NoScrollWithMouse;

        // ウィンドウサイズ初期設定
        Size = new Vector2(232, 90);
        SizeCondition = ImGuiCond.Always;

        // Plugin の Configuration を保持
		configuration = plugin.Configuration;
	}

	// IDisposable 実装（必要に応じてリソース破棄）
	public void Dispose() { }

    // PreDraw(): Draw 前の処理, ウィンドウ移動可否のフラグを設定
	public override void PreDraw()
	{
		// フラグはDraw()が呼び出される前に追加または削除する必要あり。そうしないと適用されない
		if (configuration.IsConfigWindowMovable)
		{
			Flags &= ~ImGuiWindowFlags.NoMove; // 移動可能にする
		}
		else
		{
			Flags |= ImGuiWindowFlags.NoMove; // 移動不可にする
		}
	}

    // Draw(): ウィンドウ描画処理
	public override void Draw()
	{
		// サンプルチェックボックス：SomePropertyToBeSavedAndWithADefault
		// プロパティを参照できないため、ローカルコピーを使用します
		var configValue = configuration.SomePropertyToBeSavedAndWithADefault;
		if (ImGui.Checkbox("Random Config Bool", ref configValue))
		{
			// 値変更時に設定を更新して即保存
			configuration.SomePropertyToBeSavedAndWithADefault = configValue;
			//「保存して閉じる」ボタンを提供したくない場合は、変更時すぐに保存できる
			configuration.Save();
		}

        // 設定ウィンドウを移動可能にするかのチェック
		var movable = configuration.IsConfigWindowMovable;
		if (ImGui.Checkbox("Movable Config Window", ref movable))
		{
			configuration.IsConfigWindowMovable = movable;
			configuration.Save();
		}
	}
}
