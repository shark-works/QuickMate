using System;
using ImGuiNET;
using System.IO;
using NAudio.Wave;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Utility;
using System.Collections.Generic;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Game.ClientState.Objects.Types;

namespace QuickMate;

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

    private const string CommandName = "/pmycommand";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("QuickMate");

    private ConfigWindow ConfigWindow { get; init; }
    private QuickMateWarningOverlay WarningOverlay { get; init; }

    private bool lastF3 = false;

    private WaveOutEvent? waveOut;
    private AudioFileReader? audioFile;
    private readonly string beepPath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Sounds", "Beep.wav");

    public bool showWarningText = false;
    public float warningTimer = 0f;
    private readonly float warningDuration = 10.0f;
    private bool isWarningTextActive = false;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        WarningOverlay = new QuickMateWarningOverlay(this);
        WindowSystem.AddWindow(WarningOverlay);

		PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "設定ウィンドウを表示"
        });

        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        Framework.Update += OnFrameworkUpdate;

        Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");

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

    public void Dispose()
    {
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;

        Framework.Update -= OnFrameworkUpdate;

        WindowSystem.RemoveWindow(ConfigWindow);
        ConfigWindow.Dispose();

        WindowSystem.RemoveWindow(WarningOverlay);
        WarningOverlay.Dispose();

		PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;

        CommandManager.RemoveHandler("/pmycommand");

        waveOut?.Stop();
        waveOut?.Dispose();
        audioFile?.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        ToggleConfigUi();
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();

    private unsafe void OnFrameworkUpdate(IFramework _)
    {
        bool now = KeyState[VirtualKey.F3];
        if (now && !lastF3)
        {
            if (waveOut != null && audioFile != null)
            {
                audioFile.Position = 0;
                waveOut.Play();
            }

        	isWarningTextActive = true;
        	warningTimer = warningDuration;
        	showWarningText = true;

            var am = ActionManager.Instance();
            if (am != null)
            {
                uint actionId = 29229;
                float realRecast = am->GetRecastTime(ActionType.Action, actionId);
                float realElapsed = am->GetRecastTimeElapsed(ActionType.Action, actionId);
                float remaining = Math.Abs(realRecast - realElapsed);

                bool isMounted = Condition[ConditionFlag.Mounted];
                ushort territoryId = ClientState.TerritoryType;

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
                //uint mountId = 71;
                //am->UseAction(ActionType.Mount, mountId);

                var seMessage = new SeStringBuilder()
                    .AddText($"F3: Cooldown({actionId})={remaining:0.00}s | Mounted={isMounted} | TerritoryId={territoryId} | StatusIds=[{statusIds.Trim()}]")
                    .Build();

                ChatGui.Print(new XivChatEntry
                {
                    Message = seMessage,
                    Type = XivChatType.Debug
                });
            }
            Log.Information($"F3 pressed. isWarningTextActive set to true. warningTimer: {warningTimer}");
        }
        lastF3 = now;

        if (isWarningTextActive)
        {
            warningTimer -= (float)Framework.UpdateDelta.TotalSeconds;
            if (warningTimer <= 0)
            {
                isWarningTextActive = false;
                showWarningText = false;
                Log.Information($"warningTimer reached 0. isWarningTextActive set to false. showWarningText set to false.");
            }
            else
            {
                showWarningText = true;
                Log.Debug($"Warning text active. Remaining time: {warningTimer:F2}. showWarningText: {showWarningText}");
            }
        }
        else
        {
            showWarningText = false;
        }
    }
    private void DrawWarningText()
    {
        if (!showWarningText)
        {
            return;
        }
        if (ClientState == null) { Log.Warning("DrawWarningText: ClientState is null."); return; }
        if (GameGui == null) { Log.Warning("DrawWarningText: GameGui is null."); return; }
        if (ImGui.GetCurrentContext() == IntPtr.Zero) { Log.Warning("DrawWarningText: ImGui.GetCurrentContext() is Zero."); return; }

        var player = ClientState.LocalPlayer;
        if (player == null)
        {
            Log.Warning("DrawWarningText: LocalPlayer is null.");
            return;
        }

        Vector3 worldPos = player.Position;
        worldPos.Y += 1.8f;

        Vector2 screenPos;

        if (!GameGui.WorldToScreen(worldPos, out screenPos))
        {
            Log.Warning("DrawWarningText: WorldToScreen failed for player position.");
            return;
        }

        Log.Debug($"DrawWarningText: Attempting to draw 'Warning' at screenPos ({screenPos.X:F0}, {screenPos.Y:F0})");

        var drawList = ImGui.GetBackgroundDrawList();

        if (ImGuiHelpers.MainViewport.Size.X > 0 && ImGuiHelpers.MainViewport.Size.Y > 0)
        {
            Log.Debug($"DrawWarningText: MainViewport Pos={ImGuiHelpers.MainViewport.Pos}, Size={ImGuiHelpers.MainViewport.Size}");

            drawList.PushClipRect(ImGuiHelpers.MainViewport.Pos, ImGuiHelpers.MainViewport.Pos + ImGuiHelpers.MainViewport.Size, false);

            var text = "Warning";
            var textSize = ImGui.CalcTextSize(text);

            var textRenderPos = new Vector2(screenPos.X - textSize.X / 2, screenPos.Y - textSize.Y / 2);

            Log.Debug($"DrawWarningText: Text='{text}', Size={textSize}, RenderPos={textRenderPos}");

            drawList.AddText(
                textRenderPos,
                ImGui.GetColorU32(new Vector4(1f, 0f, 0f, 1f)),
                text
            );

            drawList.PopClipRect();
        }
        else
        {
            Log.Warning($"DrawWarningText: MainViewport size is zero or less ({ImGuiHelpers.MainViewport.Size.X}, {ImGuiHelpers.MainViewport.Size.Y})");
        }
    }
}