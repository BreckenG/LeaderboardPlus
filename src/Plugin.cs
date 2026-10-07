using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GorillaNetworking;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace LeaderboardPlus;

[BepInPlugin("whys.leaderboardplus", "Leaderboard+", "0.2.0")]
public class Plugin : BaseUnityPlugin
{
    public static ConfigEntry<int> layout, theme;
    public static ConfigEntry<bool> tagView, showHz, showMs, showHost, rigs;
    public static ConfigEntry<float> left, right, bottom, top;
    public static ManualLogSource log;

    void Awake()
    {
        log = Logger;
        layout = Config.Bind("board", "layout", 0, "0 list, 1 grid, 2 big");
        theme = Config.Bind("board", "colors", 0, "0 slate, 1 wood, 2 forest, 3 terminal, 4 dusk");
        tagView = Config.Bind("board", "tagView", false, "group the board into tagged and runners");
        showHz = Config.Bind("board", "refreshRate", false, "show each players frame rate on the board");
        showMs = Config.Bind("board", "ms", false, "show each players estimated ping on the board");
        showHost = Config.Bind("board", "hostTag", false, "mark the host on the board");
        rigs = Config.Bind("board", "rigs", true, "show a 3d copy of each players rig with cosmetics");
        left = Config.Bind(
            "fit", "left", -123f, "edges of the board face in scoreboard units, about 1.1 cm each on the forest board");
        right = Config.Bind("fit", "right", 88f, "");
        bottom = Config.Bind("fit", "bottom", -140f, "");
        top = Config.Bind("fit", "top", 76f, "");
        new Harmony("whys.leaderboardplus").PatchAll();
        RoomSystem.LeftRoomEvent += new Action(Mutes.LeftRoom);
    }
}

public static class Menu
{
    public const GorillaComputer.ComputerState state = (GorillaComputer.ComputerState)100;
    static readonly string[] layouts = { "LIST", "GRID", "BIG" };
    static readonly string[] themes = { "SLATE", "WOOD", "FOREST", "TERMINAL", "DUSK" };

    static string On(ConfigEntry<bool> entry) => entry.Value ? "ON" : "OFF";

    static void Flip(ConfigEntry<bool> entry) => entry.Value = !entry.Value;

    public static string Text() =>
        "LEADERBOARD+   PRESS A NUMBER TO CHANGE\n"
        + $"\n1 LAYOUT: {layouts[Mathf.Clamp(Plugin.layout.Value, 0, 2)]}"
        + $"\n2 COLORS: {themes[Mathf.Clamp(Plugin.theme.Value, 0, 4)]}"
        + $"\n3 TAG VIEW: {On(Plugin.tagView)}"
        + $"\n4 REFRESH RATE: {On(Plugin.showHz)}"
        + $"\n5 PING MS: {On(Plugin.showMs)}"
        + $"\n6 HOST TAG: {On(Plugin.showHost)}"
        + $"\n7 3D RIGS: {On(Plugin.rigs)}";

    public static void Key(GorillaKeyboardBindings key)
    {
        if (key == GorillaKeyboardBindings.one) Plugin.layout.Value = (Mathf.Clamp(Plugin.layout.Value, 0, 2) + 1) % 3;
        else if (key == GorillaKeyboardBindings.two) Plugin.theme.Value = (Mathf.Clamp(Plugin.theme.Value, 0, 4) + 1) % 5;
        else if (key == GorillaKeyboardBindings.three) Flip(Plugin.tagView);
        else if (key == GorillaKeyboardBindings.four) Flip(Plugin.showHz);
        else if (key == GorillaKeyboardBindings.five) Flip(Plugin.showMs);
        else if (key == GorillaKeyboardBindings.six) Flip(Plugin.showHost);
        else if (key == GorillaKeyboardBindings.seven) Flip(Plugin.rigs);
        else return;
        foreach (var board in Board.all) board.Restyle();
    }
}

public static class Net
{
    static Dictionary<VRRig, float> pings = new();

    public static void Tap(VRRig rig, double sent)
    {
        if (rig.isLocal) return;
        var ms = (float)((PhotonNetwork.Time - sent) * 1000.0);
        if (ms < 0f || ms > 2000f) return;
        pings[rig] = pings.TryGetValue(rig, out var old) ? old + (ms - old) * 0.1f : ms;
    }

    public static int Ms(VRRig rig)
    {
        if (rig.isLocal) return PhotonNetwork.GetPing();
        return pings.TryGetValue(rig, out var ms) ? (int)ms : -1;
    }

    public static int Hz(VRRig rig) => rig.isLocal ? Mathf.RoundToInt(1f / Time.smoothDeltaTime) : rig.fps;
}

public static class Mutes
{
    public static readonly string[] modes = { "ALWAYS", "ROOM", "5 MIN" };
    public static int mode;
    public static Dictionary<string, float> timed = new(), volumes = new();
    static HashSet<string> roomOnly = new();

    public static void Toggle(GorillaPlayerScoreboardLine line)
    {
        var button = line.muteButton;
        button.isOn = !button.isAutoOn && !button.isOn;
        line.PressButton(button.isOn, GorillaPlayerLineButton.ButtonType.Mute);
        var id = line.linePlayer.UserId;
        roomOnly.Remove(id);
        timed.Remove(id);
        if (!button.isOn) return;
        if (mode == 1) roomOnly.Add(id);
        else if (mode == 2) timed[id] = Time.time + 300f;
    }

    public static void LeftRoom()
    {
        foreach (var id in roomOnly) PlayerPrefs.DeleteKey(id);
        foreach (var id in timed.Keys) PlayerPrefs.DeleteKey(id);
        roomOnly.Clear();
        timed.Clear();
        volumes.Clear();
    }
}

[HarmonyPatch(typeof(GorillaComputer), "Initialise")]
class ComputerInit
{
    static void Prefix(GorillaComputer __instance)
    {
        if (__instance.OrderList.Exists(item => item.State == Menu.state)) return;
        __instance.OrderList.Add(new GorillaComputer.StateOrderItem(Menu.state, "Leaderboard"));
    }
}

[HarmonyPatch(typeof(GorillaComputer), "UpdateScreen")]
class ComputerScreen
{
    static void Postfix(GorillaComputer __instance)
    {
        if (__instance.currentState == Menu.state) __instance.screenText.Set(Menu.Text());
    }
}

[HarmonyPatch(typeof(GorillaComputer), "PressButton")]
class ComputerKeys
{
    static void Prefix(GorillaComputer __instance, GorillaKeyboardBindings buttonPressed)
    {
        if (__instance.currentState == Menu.state) Menu.Key(buttonPressed);
    }
}

[HarmonyPatch(typeof(GorillaScoreBoard), "RedrawPlayerLines")]
class Redraw
{
    static void Postfix(GorillaScoreBoard __instance)
    {
        var board = Board.Get(__instance);
        if (board != null) board.Sync();
    }
}

[HarmonyPatch(typeof(VRRig), "AddVelocityToQueue")]
class PingTap
{
    static void Postfix(VRRig __instance, double serverTime) => Net.Tap(__instance, serverTime);
}

[HarmonyPatch(typeof(VRRig), "LocalUpdateCosmeticsWithTryon")]
class CosmeticsChanged
{
    static void Postfix(VRRig __instance) => Statue.Dirty(__instance);
}
