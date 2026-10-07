using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

namespace LeaderboardPlus;

public class Panel
{
    public const float width = 66f;
    const string white = "<color=#ffffff>";
    static readonly GorillaPlayerLineButton.ButtonType[] reasons =
    {
        GorillaPlayerLineButton.ButtonType.HateSpeech,
        GorillaPlayerLineButton.ButtonType.Toxicity,
        GorillaPlayerLineButton.ButtonType.Cheating
    };
    static readonly string[] reasonNames = { "HATE SPEECH", "TOXICITY", "CHEATING" };
    static StringBuilder builder = new(120);

    public Statue statue = new();
    public bool busy;
    Board board;
    Transform root, ring, tick1, tick2, knob, fill;
    TextMeshPro title, info, status, volumeText;
    Btn prev, next, mute, report, close;
    Btn[] lengths = new Btn[3], whys = new Btn[3], hosts = new Btn[3];
    MeshRenderer[] arcs = new MeshRenderer[28];
    GameObject slider;
    float stageY, front, travel, shownVolume = -1f;
    int mode, volumeStep = -1, volumePercent = -1;
    string action, shownInfo, shownTitle, shownStatus;

    public Panel(Board owner)
    {
        board = owner;
        float topY = board.top - 3f, bottomY = board.bottom + 24f, height = topY - bottomY, inner = width - 6f, up = height / 2f;
        root = new GameObject("panel").transform;
        root.SetParent(board.root, false);
        root.localPosition = new Vector3(board.left + 3f + width / 2f, (topY + bottomY) / 2f, -2.2f);
        board.Solid(root, board.cardMat, 1, Skin.Slab(width, height, 2.4f, 1.8f, board.look.jit, board.look.wob, 5));
        front = -2.4f - board.look.jit - 0.3f;
        float keyZ = board.keyZ, textZ = board.textZ;
        board.keyZ = front;
        board.textZ = front - 0.4f;

        prev = board.MakeBtn(root, -inner / 2f + 6f, up - 9f, 12f, 12f, leftHand => board.Step(-1, leftHand));
        next = board.MakeBtn(root, inner / 2f - 6f, up - 9f, 12f, 12f, leftHand => board.Step(1, leftHand));
        title = board.Label(root);
        board.Box(title, 0f, up - 9f, inner - 28f, 13f, 0.85f, TextAlignmentOptions.Center);
        stageY = up - 45f;
        board.Solid(root, board.darkMat, 2, Skin.Round(inner, 56f, 4f), 0f, stageY, front - 0.05f);
        info = board.Label(root);
        board.Box(info, 0f, up - 79f, inner, 9f, 0.5f, TextAlignmentOptions.Center);
        mute = board.MakeBtn(root, 0f, up - 92f, inner, 14f, OnMute);
        BuildSlider(inner, up - 108f);
        var third = (inner - 4f) / 3f;
        for (var i = 0; i < 3; i++)
        {
            var index = i;
            var x = -inner / 2f + third / 2f + i * (third + 2f);
            lengths[i] = board.MakeBtn(root, x, up - 121f, third, 9f, leftHand => OnLength(index, leftHand));
            whys[i] = board.MakeBtn(root, 0f, up - 91f - i * 15f, inner, 12.5f, leftHand => OnWhy(index, leftHand));
            hosts[i] = board.MakeBtn(root, x, up - 152f, third, 12f, leftHand => OnHost(index, leftHand));
        }
        report = board.MakeBtn(root, 0f, up - 136f, inner, 13f, OnReport);
        status = board.Label(root);
        board.Box(status, 0f, up - 166f, inner, 10f, 0.55f, TextAlignmentOptions.Center);
        close = board.MakeBtn(root, 0f, -up + 9f, inner, 11f, leftHand => board.Select(-1, leftHand));
        BuildRing();
        board.keyZ = keyZ;
        board.textZ = textZ;
    }

    void BuildSlider(float inner, float y)
    {
        travel = inner - 12f;
        slider = new GameObject("vol") { layer = board.touchLayer };
        slider.transform.SetParent(root, false);
        slider.transform.localPosition = new Vector3(0f, y, front);
        board.Solid(slider.transform, board.darkMat, 4, Skin.Slab(inner, 5f, 0.7f, 0.7f, 0f, 0f, 2));
        fill = board.Solid(slider.transform, board.accentMat, 5, Skin.Round(1f, 3f, 0f)).transform;
        knob = new GameObject("knob").transform;
        knob.SetParent(slider.transform, false);
        board.Solid(knob, board.keyMat, 6, Skin.Slab(12f, 10f, 2f, 1f, 0f, 0f, 3));
        volumeText = board.Label(knob);
        board.Box(volumeText, 0f, 0f, 11f, 8f, 0.36f, TextAlignmentOptions.Center, -2.35f);
        var touch = slider.AddComponent<BoxCollider>();
        touch.isTrigger = true;
        touch.center = new Vector3(0f, 0f, -1.5f);
        touch.size = new Vector3(inner, 12f, Board.line * 0.7f);
        var drag = slider.AddComponent<Slider>();
        drag.board = board;
        drag.width = travel;
        drag.onMove = OnVolume;
    }

    void BuildRing()
    {
        const float radius = 17f, thick = 2.4f;
        ring = new GameObject("ring").transform;
        ring.SetParent(root, false);
        ring.localPosition = new Vector3(0f, stageY, front - 3f);
        var arc = Skin.Round(radius * 2f * Mathf.PI / arcs.Length * 1.2f, thick, 0f);
        for (var i = 0; i < arcs.Length; i++)
        {
            var angle = Mathf.PI / 2f - i * Mathf.PI * 2f / arcs.Length;
            arcs[i] = board.Solid(ring, board.accentMat, 9, arc, Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            arcs[i].transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 90f);
        }
        tick1 = Bar(new Vector2(-0.45f, 0f) * radius, new Vector2(-0.12f, -0.33f) * radius, thick);
        tick2 = Bar(new Vector2(-0.12f, -0.33f) * radius, new Vector2(0.48f, 0.36f) * radius, thick);
        ring.gameObject.SetActive(false);
    }

    Transform Bar(Vector2 from, Vector2 to, float thick)
    {
        var pivot = new GameObject("bar").transform;
        var along = to - from;
        pivot.SetParent(ring, false);
        pivot.localPosition = from;
        pivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
        board.Solid(pivot, board.accentMat, 9, Skin.Round(along.magnitude, thick, 0f), along.magnitude / 2f);
        return pivot;
    }

    public void Destroy()
    {
        statue.Kill();
        Object.Destroy(root.gameObject);
    }

    void OnMute(bool leftHand)
    {
        var row = board.Picked();
        if (row == null) return;
        Mutes.Toggle(row.line);
        board.Play(row.line.muteButton.isOn ? Synth.down : Synth.pluck, leftHand);
        Refresh();
    }

    void OnLength(int index, bool leftHand)
    {
        Mutes.mode = index;
        board.Play(Synth.tick, leftHand);
        Refresh();
    }

    void OnVolume(float volume, bool leftHand)
    {
        var row = board.Picked();
        if (row == null) return;
        Mutes.volumes[row.line.linePlayer.UserId] = volume;
        var rig = row.line.playerVRRig;
        if (rig != null && rig.voiceAudio != null) rig.voiceAudio.volume = volume;
        ShowVolume(volume);
        var step = Mathf.RoundToInt(volume * 10f);
        if (step == volumeStep) return;
        volumeStep = step;
        board.Play(Synth.n1 + Mathf.Min(3, (int)(volume * 4f)), leftHand);
    }

    void ShowVolume(float volume)
    {
        if (volume == shownVolume) return;
        shownVolume = volume;
        float x = (volume - 0.5f) * travel, start = -travel / 2f - 4f;
        knob.localPosition = new Vector3(x, 0f, -0.7f);
        fill.localScale = new Vector3(Mathf.Max(x - start, 0.01f), 1f, 1f);
        fill.localPosition = new Vector3((x + start) / 2f, 0f, -0.75f);
        var percent = Mathf.RoundToInt(volume * 100f);
        if (percent == volumePercent) return;
        volumePercent = percent;
        volumeText.text = $"<color={board.pal.keyText}>{percent}";
    }

    void OnReport(bool leftHand)
    {
        mode = mode == 1 ? 0 : 1;
        board.Play(mode == 1 ? Synth.tap : Synth.down, leftHand);
        Refresh();
    }

    void OnWhy(int index, bool leftHand)
    {
        var row = board.Picked();
        if (row == null) return;
        board.Play(Synth.tap, leftHand);
        var name = board.Name(row.line);
        row.line.SetReportState(false, reasons[index]);
        board.StartCoroutine(Done($"REPORTED {name}", leftHand));
    }

    void OnHost(int index, bool leftHand)
    {
        var row = board.Picked();
        if (row == null) return;
        if (mode != 2)
        {
            board.Play(Synth.tap, leftHand);
            if (index == 0) row.line.PressButton(false, GorillaPlayerLineButton.ButtonType.MuteAllRoom);
            else Ask(index == 1 ? "KICK" : "BLOCK");
            Refresh();
            return;
        }
        board.Play(index == 0 ? Synth.tap : Synth.down, leftHand);
        if (index != 0)
        {
            mode = 0;
            Refresh();
            return;
        }
        var actor = row.line.linePlayer.ActorNumber;
        if (action == "KICK") RoomControls.KickPlayer(actor);
        else RoomControls.KickAndBlockPlayer(actor);
        board.StartCoroutine(Done((action == "KICK" ? "KICKED " : "BLOCKED ") + board.Name(row.line), leftHand));
    }

    void Ask(string what)
    {
        action = what;
        mode = 2;
    }

    public void Pick()
    {
        mode = 0;
        statue.Kill();
        Refresh();
    }

    static void Set(TextMeshPro label, ref string shown, string text)
    {
        if (text == shown) return;
        shown = text;
        label.text = text;
    }

    void Actions(bool main, bool reporting, bool hostRow, bool confirming)
    {
        mute.Show(main);
        if (slider.activeSelf != main) slider.SetActive(main);
        for (var i = 0; i < 3; i++)
        {
            lengths[i].Show(main);
            whys[i].Show(reporting);
            hosts[i].Show(confirming ? i < 2 : hostRow);
        }
        report.Show(main || reporting);
        close.Show(main);
    }

    string Info(Row row, string dim)
    {
        var rig = row.line.playerVRRig;
        builder.Clear();
        if (row.me) return builder.Append(dim).Append("THIS IS YOU").ToString();
        if (rig != null)
        {
            builder.Append("<color=").Append(board.pal.accentHex).Append('>').Append(Net.Hz(rig)).Append(" HZ</color>");
            var ms = Net.Ms(rig);
            if (ms >= 0) builder.Append(dim).Append("  ").Append(ms).Append(" MS</color>");
        }
        if (row.tagged) builder.Append("  <color=#ff6a2b>TAGGED</color>");
        if (Plugin.showHost.Value && row.line.linePlayer.IsMasterClient)
            builder.Append("  <color=").Append(board.pal.gold).Append(">HOST</color>");
        return builder.ToString();
    }

    public void Refresh()
    {
        if (busy) return;
        var pal = board.pal;
        string fg = $"<color={pal.fg}>", dim = $"<color={pal.dim}>", key = $"<color={pal.keyText}>";
        prev.Text($"{key}<");
        next.Text($"{key}>");
        close.Text($"{key}CLOSE");
        var row = board.Picked();
        if (row == null || row.line == null || row.line.linePlayer == null)
        {
            mode = 0;
            Set(title, ref shownTitle, $"{fg}{board.order.Count} PLAYERS");
            Set(info, ref shownInfo, $"{dim}TAP A NAME OR USE THE ARROWS");
            Set(status, ref shownStatus, "");
            Actions(false, false, false, false);
            return;
        }
        var name = board.Name(row.line);
        Set(title, ref shownTitle, fg + name);
        Set(info, ref shownInfo, Info(row, dim));
        if (row.me)
        {
            Set(status, ref shownStatus, "");
            Actions(false, false, false, false);
            close.Show(true);
            return;
        }
        Keys(row, name, fg, dim, key);
        ShowRig(row.line.playerVRRig);
    }

    void Keys(Row row, string name, string fg, string dim, string key)
    {
        var ink = $"<color={board.pal.ink}>";
        var id = row.line.linePlayer.UserId;
        bool main = mode == 0, reporting = mode == 1, confirming = mode == 2;
        var muted = row.line.muteButton.isOn || row.line.muteButton.isAutoOn;
        Actions(main, reporting, main && RoomControls.CanModerate(), confirming);
        mute.Text(muted ? $"{white}MUTED" : $"{key}MUTE");
        mute.Face(muted ? board.dangerMat : board.keyMat);
        volumePercent = -1;
        shownVolume = -1f;
        ShowVolume(Mutes.volumes.TryGetValue(id, out var volume) ? volume : 1f);
        for (var i = 0; i < 3; i++)
        {
            lengths[i].Face(i == Mutes.mode ? board.accentMat : board.keyMat);
            lengths[i].Text((i == Mutes.mode ? ink : key) + Mutes.modes[i]);
            whys[i].Text(white + reasonNames[i]);
            whys[i].Face(board.dangerMat);
        }
        report.Text(reporting ? $"{key}CANCEL" : $"{white}REPORT");
        report.Face(reporting ? board.keyMat : board.dangerMat);
        hosts[0].Face(confirming ? board.dangerMat : board.keyMat);
        if (confirming)
        {
            hosts[0].Text($"{white}YES");
            hosts[1].Text($"{key}NO");
            hosts[1].Face(board.keyMat);
            Set(status, ref shownStatus, $"{fg}{action} {name}?");
            return;
        }
        hosts[0].Text(key + (RoomControls.MutedPlayers.ContainsKey(id) ? "UNSILENCE" : "SILENCE"));
        hosts[1].Text($"{key}KICK");
        hosts[2].Text($"{key}BLOCK");
        Set(status, ref shownStatus, reporting ? $"{dim}WHAT ARE THEY DOING?" : "");
    }

    void ShowRig(VRRig rig)
    {
        if (!Plugin.rigs.Value || rig == null) return;
        if (statue.rig == rig && !statue.Stale())
        {
            statue.SyncMats();
            return;
        }
        statue.Build(rig, root);
        statue.Fit(new Vector3(0f, stageY - 1f, front - 0.4f), width - 22f, 50f);
    }

    public void Tick(float now)
    {
        if (statue.holder == null) return;
        var turn = Quaternion.Euler(0f, Mathf.Sin(now * 0.7f) * 35f, 0f);
        statue.holder.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up) * turn;
    }

    IEnumerator Done(string text, bool leftHand)
    {
        busy = true;
        Actions(false, false, false, false);
        Set(status, ref shownStatus, $"<color={board.pal.fg}>{text}");
        if (statue.holder != null) statue.holder.gameObject.SetActive(false);
        ring.gameObject.SetActive(true);
        ring.localScale = Vector3.one;
        foreach (var arc in arcs) arc.enabled = false;
        tick2.localScale = new Vector3(0.0001f, 1f, 1f);
        tick1.localScale = tick2.localScale;

        var t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.34f;
            var lit = (int)(Mathf.SmoothStep(0f, 1f, t) * arcs.Length + 0.5f);
            for (var i = 0; i < arcs.Length; i++) arcs[i].enabled = i < lit;
            yield return null;
        }
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.2f;
            var eased = 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
            tick1.localScale = new Vector3(Mathf.Max(Mathf.Clamp01(eased / 0.4f), 0.0001f), 1f, 1f);
            tick2.localScale = new Vector3(Mathf.Max(Mathf.Clamp01((eased - 0.4f) / 0.6f), 0.0001f), 1f, 1f);
            yield return null;
        }
        board.Play(Synth.confirm, leftHand);
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.28f;
            ring.localScale = Vector3.one * (1f + 0.16f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)));
            yield return null;
        }
        yield return new WaitForSeconds(0.9f);
        ring.gameObject.SetActive(false);
        if (statue.holder != null) statue.holder.gameObject.SetActive(true);
        busy = false;
        mode = 0;
        if (board.Picked() == null) board.Close();
        else Refresh();
    }
}
