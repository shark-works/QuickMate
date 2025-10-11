using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks; // Task.Runのために追加
using Dalamud.Plugin.Services;
using NAudio.Wave; // DirectSoundOut を使うなら NAudio.DirectSound も必要になるかも

namespace ScouterX;

public sealed class AudioManager : IDisposable
{
    private readonly IPluginLog _log;
    private readonly Dictionary<string, byte[]> _soundCache = new();
    
    // 現在アクティブな再生を管理するためのリスト
    // ConcurrentBagなどスレッドセーフなコレクションの方が理想的だが、Listで実装例を示す
    private readonly List<PlaybackInstance> _activePlaybacks = new(); 
    private readonly object _playbackListLock = new(); // _activePlaybacksのロック

    public AudioManager(IPluginLog log)
    {
        _log = log;
        PreloadAllSounds();
    }

    public void Dispose()
    {
        // すべてのアクティブな再生を停止し、リソースを解放
        lock (_playbackListLock)
        {
            foreach (var instance in _activePlaybacks)
            {
                try { instance.WaveOut?.Stop(); } catch { }
                try { instance.WaveOut?.Dispose(); } catch { }
                try { instance.MemoryStream?.Dispose(); } catch { }
                try { instance.Reader?.Dispose(); } catch { }
            }
            _activePlaybacks.Clear();
        }

        _soundCache.Clear();
        _log.Information("AudioManager disposed.");
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
                    _log.Warning($"Resource stream not found: {resName}");
                    continue;
                }
                using var mem = new MemoryStream();
                stream.CopyTo(mem);
                string key = Path.GetFileName(resName);
                _soundCache[key] = mem.ToArray();
            }
            _log.Information($"Embedded {_soundCache.Count} sound(s) preloaded from resources.");
        }
        catch (Exception ex)
        {
            _log.Error($"Error preloading embedded sounds: {ex.Message}");
        }
    }

    public void PlaySoundByName(string fileName)
    {
        // 再生処理をバックグラウンドスレッドにオフロード
        Task.Run(() =>
        {
            byte[]? soundData;
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string? resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

                if (resourceName == null)
                {
                    _log.Warning($"Embedded sound not found: {fileName}");
                    return;
                }

                if (!_soundCache.TryGetValue(Path.GetFileName(resourceName), out soundData))
                {
                    _log.Warning($"Sound data not found in cache for {fileName}. Attempting to load directly.");
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream == null)
                    {
                        _log.Warning($"Resource stream not found for {resourceName}");
                        return;
                    }
                    using var mem = new MemoryStream();
                    stream.CopyTo(mem);
                    soundData = mem.ToArray();
                    // キャッシュに追加 (ただし、競合状態を避けるため、通常はPreloadAllSoundsで全てキャッシュ済みとする)
                    // lock (_soundCacheLock) { _soundCache[Path.GetFileName(resourceName)] = soundData; }
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error preparing sound '{fileName}': {ex.Message}");
                return;
            }

            if (soundData == null) return;

            // 各再生は独立したPlaybackInstanceを持つ
            var playbackInstance = new PlaybackInstance();

            try
            {
                playbackInstance.MemoryStream = new MemoryStream(soundData);
                playbackInstance.MemoryStream.Position = 0;
                
                // NAudio.Wave.WaveFileReader を使用
                playbackInstance.Reader = new WaveFileReader(playbackInstance.MemoryStream); 
                
                // NAudio.Wave.WaveOutEvent を使用
                playbackInstance.WaveOut = new WaveOutEvent(); 
                
                playbackInstance.WaveOut.Init(playbackInstance.Reader);

                lock (_playbackListLock)
                {
                    _activePlaybacks.Add(playbackInstance);
                }

                playbackInstance.WaveOut.Play();
                _log.Debug($"Sound '{fileName}' started playing.");

                playbackInstance.WaveOut.PlaybackStopped += (_, _) =>
                {
                    lock (_playbackListLock)
                    {
                        // 再生が完了したらリストから削除し、リソースを解放
                        _activePlaybacks.Remove(playbackInstance);
                    }
                    try { playbackInstance.Reader?.Dispose(); } catch { _log.Warning("Reader dispose error after playback."); }
                    try { playbackInstance.MemoryStream?.Dispose(); } catch { _log.Warning("MemoryStream dispose error after playback."); }
                    try { playbackInstance.WaveOut?.Dispose(); } catch { _log.Warning("WaveOut dispose error after playback."); }
                    _log.Debug($"Sound '{fileName}' playback stopped and resources released.");
                };
            }
            catch (Exception ex)
            {
                _log.Error($"Error playing embedded sound '{fileName}': {ex.Message}");
                // エラー発生時は即座にリソースを解放
                try { playbackInstance.Reader?.Dispose(); } catch { }
                try { playbackInstance.MemoryStream?.Dispose(); } catch { }
                try { playbackInstance.WaveOut?.Dispose(); } catch { }
                lock (_playbackListLock)
                {
                    _activePlaybacks.Remove(playbackInstance);
                }
            }
        });
    }

    // PlaybackInstanceのプライベートクラスを定義
    private class PlaybackInstance
    {
        public WaveOutEvent? WaveOut { get; set; }
        public MemoryStream? MemoryStream { get; set; }
        public WaveFileReader? Reader { get; set; } // WaveFileReader も管理対象に追加
    }

    // ウォームアップ時に使用する可能性のある強制停止メソッド（オプション）
    public void StopAllSounds()
    {
        lock (_playbackListLock)
        {
            foreach (var instance in _activePlaybacks.ToList()) // ToList() でコピーしてループ中に変更可能にする
            {
                try { instance.WaveOut?.Stop(); } catch { }
                // Stop()を呼ぶとPlaybackStoppedイベントが発火し、そこでDisposeされる想定
                // なので、ここではリストから削除するだけでOK
            }
        }
    }
}