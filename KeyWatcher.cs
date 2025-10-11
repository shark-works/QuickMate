using System;
using System.Linq; // ★この行は引き続き必要です (LINQを直接使わない場合でも、他の部分で必要になる可能性もあるため)★
using System.Collections.Generic;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Game.ClientState.Conditions;

namespace ScouterX;

public class KeyWatcher : IDisposable
{
    private readonly IKeyState _keyState;
    private readonly IPluginLog _log;
    private readonly IChatGui _chatGui;
    private readonly IClientState _clientState; // F3キー処理でTerritoryTypeやActionManager.Instance()のために必要
    private readonly ICondition _condition;     // F3キー処理でMounted状態のために必要
    private readonly AudioManager _audioManager; // AudioManagerへの参照を保持
    private readonly Plugin _plugin; // Pluginへの参照を保持
    private readonly StatusWatcher _statusWatcher; // ★StatusWatcherへの参照を追加★

    private readonly bool[] _keyPressStates = new bool[(int)VirtualKey.F12 + 1];

    public KeyWatcher(IKeyState keyState, IPluginLog log, IChatGui chatGui, IClientState clientState, ICondition condition, AudioManager audioManager, Plugin plugin, StatusWatcher statusWatcher) // ★コンストラクタにStatusWatcherを追加★
    {
        _keyState = keyState;
        _log = log;
        _chatGui = chatGui;
        _clientState = clientState;
        _condition = condition;
        _audioManager = audioManager;
        _plugin = plugin;
        _statusWatcher = statusWatcher; // ★StatusWatcherのインスタンスを保持★
    }

    public void Dispose()
    {
        // イベントハンドラーの解除は不要（FrameworkManager側でUpdateが解除されるため）
    }

    // FrameworkManagerから呼び出されるメソッド
    public void HandleKeys()
    {
        HandleKeyPressEvent(VirtualKey.F1, HandleF1KeyPress);
        HandleKeyPressEvent(VirtualKey.F3, HandleF3KeyPress);
        HandleKeyPressEvent(VirtualKey.F4, HandleF4KeyPress);
        HandleKeyPressEvent(VirtualKey.F5, HandleF5KeyPress);
    }

    private void HandleKeyPressEvent(VirtualKey key, Action? onPressed)
    {
        int keyIndex = (int)key;
        if (keyIndex >= _keyPressStates.Length || keyIndex < 0)
        {
            _log.Warning($"Invalid VirtualKey index: {keyIndex} for key {key}. Skipping key press handling.");
            return;
        }

        bool nowState = _keyState[key];

        if (nowState && !_keyPressStates[keyIndex])
            onPressed?.Invoke();

        _keyPressStates[keyIndex] = nowState;
    }

    // ====== F1 ======
    private void HandleF1KeyPress()
    {
        _audioManager.PlaySoundByName("warning.wav");
        _plugin.isF1TextActive = true;
        _plugin.f1Timer = _plugin.f1Duration;
        _plugin.showF1Text = true;
        _chatGui.Print(new XivChatEntry
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
        _plugin.isF3TextActive = true;
        _plugin.f3Timer = _plugin.f3Duration;
        _plugin.showF3Text = true;

        var am = ActionManager.Instance();
        if (am == null)
        {
            return;
        }

        uint actionId = 29229;
        float realRecast = am->GetRecastTime(ActionType.Action, actionId);
        float realElapsed = am->GetRecastTimeElapsed(ActionType.Action, actionId);
        float remaining = Math.Abs(realRecast - realElapsed);

        bool isMounted = _condition[ConditionFlag.Mounted];
        ushort territoryId = _clientState.TerritoryType;
        string statusIds = _statusWatcher.GetLocalPlayerStatusIds(); // ★StatusWatcherから取得★

        uint mountId = 71;
        am->UseAction(ActionType.Mount, mountId);

        var seMessage = new SeStringBuilder()
            .AddText(
                $"F3: Cooldown({actionId})={remaining:0.00}s | " +
                $"Mounted={isMounted} | " +
                $"TerritoryId={territoryId} | " +
                $"StatusIds=[{statusIds.Trim()}]"
            ).Build();
        _chatGui.Print(new XivChatEntry
        {
            Message = seMessage,
            Type = XivChatType.Debug
        });
    }

    // ====== F4 ======
    private void HandleF4KeyPress()
    {
        _audioManager.PlaySoundByName("alert.wav");
        _plugin.isF4TextActive = true;
        _plugin.f4Timer = _plugin.f4Duration;
        _plugin.showF4Text = true;
        _chatGui.Print(new XivChatEntry
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
        _plugin.isF5Running = true;
        _plugin.showF5Timer = true;
        _plugin.f5Remaining = 60f;

        _chatGui.Print(new XivChatEntry
        {
            Message = new SeStringBuilder().AddText("[ScouterX] カウントダウン開始 (60秒)").Build(),
            Type = XivChatType.Debug
        });
    }
}