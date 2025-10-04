using Dalamud.Configuration;
using System;

namespace QuickMate;

// QuickMate プラグインの設定を保存するクラス
// Dalamud が JSON として自動的に保存・読み込み
[Serializable] // 設定オブジェクトをシリアライズ可能にする
public class Configuration : IPluginConfiguration
{
	// IPluginConfiguration で必須のプロパティ
	// 設定ファイルのバージョン。将来フォーマット変更時に使用
    public int Version { get; set; } = 0;

	// 設定ウィンドウをドラッグで移動できるか
	public bool IsConfigWindowMovable { get; set; } = true;

	// 例：何かの機能を有効/無効にするフラグ
	public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;

    // 設定を保存するメソッド
	// PluginInterface.SavePluginConfig(this) を呼ぶことで設定を永続化
	// 以下は保存の手間を軽減するためのもの
	public void Save()
	{
		// Plugin.cs 内で public static PluginInterface を定義している想定
		Plugin.PluginInterface.SavePluginConfig(this);
	}
}
