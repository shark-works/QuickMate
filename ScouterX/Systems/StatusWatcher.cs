using System;
using System.Linq; // LINQを使用するため
using System.Collections.Generic;
using Dalamud.Plugin.Services; // IClientStateを使用するため

namespace ScouterX.Systems;

public class StatusWatcher : IDisposable
{
    private readonly IClientState _clientState;
    private readonly IPluginLog _log; // 必要に応じてログ出力

    public StatusWatcher(IClientState clientState, IPluginLog log)
    {
        _clientState = clientState;
        _log = log;
    }

    public void Dispose()
    {
        // 現時点では特別なクリーンアップは不要
    }

    /// <summary>
    /// ローカルプレイヤーの現在のアクティブなステータスIDのリストを文字列で取得します。
    /// </summary>
    /// <returns>ステータスIDをスペース区切りで連結した文字列。プレイヤーがいない場合は空文字列。</returns>
    public string GetLocalPlayerStatusIds()
    {
        if (_clientState.LocalPlayer == null)
        {
            return string.Empty;
        }
        // StatusListはFFXIVClientStructs.FFXIV.Client.Game.Object.GameObject.StatusListを指す
        // その中にStatusIdを持つStatusクラスのインスタンスが含まれていると仮定
        return string.Join(" ", _clientState.LocalPlayer.StatusList
            .Where(s => s.StatusId != 0) // StatusIdが0でないもののみをフィルタリング
            .Select(s => s.StatusId.ToString())); // IDを文字列に変換
    }

    // 今後、ターゲットのステータス監視など、他のステータス関連の機能を追加する予定
}