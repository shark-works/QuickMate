// ====== usingディレクティブ ======
using System;
using Dalamud.Plugin.Services;

namespace ScouterX;

public class FrameworkManager : IDisposable
{
    private readonly IFramework _framework;
    private readonly Plugin _plugin; // Pluginへの参照を保持

    public FrameworkManager(IFramework framework, Plugin plugin)
    {
        _framework = framework;
        _plugin = plugin; // Pluginのインスタンスを受け取る
        _framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        _framework.Update -= OnFrameworkUpdate;
    }

    private unsafe void OnFrameworkUpdate(IFramework _)
    {
        // キープレスイベントのハンドリングはKeyWatcherに移動
        // ただし、タイマー更新はPluginのデータに依存するため、FrameworkManagerがPluginインスタンスを介して更新する
        float delta = (float)_framework.UpdateDelta.TotalSeconds;

        _plugin.UpdateTextDisplayTimer(ref _plugin.isF1TextActive, ref _plugin.showF1Text, ref _plugin.f1Timer, _plugin.f1Duration, delta);
        _plugin.UpdateTextDisplayTimer(ref _plugin.isF3TextActive, ref _plugin.showF3Text, ref _plugin.f3Timer, _plugin.f3Duration, delta);
        _plugin.UpdateTextDisplayTimer(ref _plugin.isF4TextActive, ref _plugin.showF4Text, ref _plugin.f4Timer, _plugin.f4Duration, delta);

        // F5カウントダウンタイマー更新
        if (_plugin.isF5Running)
        {
            _plugin.f5Remaining -= delta;
            if (_plugin.f5Remaining < 0f)
                _plugin.f5Remaining = 0f;

            if (_plugin.f5Remaining == 0f)
            {
                _plugin.isF5Running = false;
                _plugin.showF5Timer = false;
            }
        }
    }
}