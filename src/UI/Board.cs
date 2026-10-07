using System;
using System.Collections.Generic;
using System.Text;
using GorillaTagScripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LeaderboardPlus;

public class Row
{
    public int actor, index, stamp;
    public GorillaPlayerScoreboardLine line;
    public Transform root, body;
    public MeshRenderer plank, frame, dot;
    public TextMeshPro label;
    public BoxCollider touch;
    public Statue statue = new();
    public Vector3 pos, vel, target;
    public float w, h, rigX, rigY, rigW, rigH, appear, flash, statueDue;
    public bool tile, leaving, hidden, tagged, primed, me, speaking, muted, fresh = true;
    public string text;
}

public struct Pal
{
    public int bg, card, row, alt, accent, tag, key, dark, danger;
    public string fg, dim, gold, accentHex, ink, keyText;
}

public class Board : MonoBehaviour
{
    public const float line = 14f, charWidth = 5.3f, rowZ = -2.7f;
    public static List<Board> all = new();
    static StringBuilder builder = new(160);
    static List<Row> flow = new();
    static GorillaScoreBoard reportBoard;
    static bool reportLooked;
    static readonly Pal[] pals =
    {
        new()
        {
            bg = 0x39424e, card = 0x4a5563, row = 0x566272, alt = 0x4f5b6a, accent = 0x6ad1ff, tag = 0x8a3a22, key = 0x7b8898,
            dark = 0x0c0f13, danger = 0xd8452d, fg = "#f2f4f7", dim = "#b4bfcc", gold = "#ffc600", accentHex = "#6ad1ff",
            ink = "#0c1014", keyText = "#f2f4f7"
        },
        new()
        {
            bg = 0x7a5535, card = 0x8f653e, row = 0x9a6d42, alt = 0x8a6038, accent = 0xffd24a, tag = 0xa83c1c, key = 0xe6dfd0,
            dark = 0x24160b, danger = 0xe2432a, fg = "#fff3d6", dim = "#e8cfa6", gold = "#ffd84a", accentHex = "#ffd24a",
            ink = "#2a1a0c", keyText = "#2a1a0c"
        },
        new()
        {
            bg = 0x2c5a45, card = 0x356b52, row = 0x7a5a38, alt = 0x6e5032, accent = 0x9dff8a, tag = 0x9a3a1c, key = 0xd9e6c3,
            dark = 0x08140f, danger = 0xd8452d, fg = "#f1efe6", dim = "#cfe0c8", gold = "#ffd24a", accentHex = "#9dff8a",
            ink = "#08140f", keyText = "#08140f"
        },
        new()
        {
            bg = 0x0e1a11, card = 0x16281a, row = 0x12301a, alt = 0x153820, accent = 0x6dff8a, tag = 0x4a2010, key = 0x1f4a2a,
            dark = 0x020503, danger = 0xb8381f, fg = "#d6ffdc", dim = "#7fc08c", gold = "#ffe066", accentHex = "#6dff8a",
            ink = "#031006", keyText = "#d6ffdc"
        },
        new()
        {
            bg = 0x3a2c58, card = 0x4a3970, row = 0x58448a, alt = 0x503e7c, accent = 0xffa24a, tag = 0x8a3020, key = 0x7a62b0,
            dark = 0x0e0a16, danger = 0xd8452d, fg = "#f6f0ff", dim = "#c9bde6", gold = "#ffd24a", accentHex = "#ffa24a",
            ink = "#160f22", keyText = "#f6f0ff"
        }
    };

    public Transform root;
    public Pal pal;
    public Look look;
    public Material cardMat, accentMat, keyMat, darkMat, dangerMat;
    public List<Row> order = new();
    public float left, bottom, top, keyZ, textZ = -2.9f;
    public int touchLayer;
    GorillaScoreBoard vanilla;
    Material bgMat, rowMat, altMat, tagMat, lavaMat, whiteMat;
    TMP_FontAsset font;
    Material fontMat;
    List<Row> rows = new();
    Dictionary<int, Row> byActor = new();
    List<GameObject> studs = new();
    TextMeshPro header, taggedTitle, runnersTitle;
    MeshRenderer slab;
    Btn next;
    AudioSource speaker;
    Panel panel;
    bool ready, awake;
    float right, listLeft, listRight, listTop, listBottom, fontSize, lastPress, nextWake, nextSlow, nextText, nextPage;
    int stamp, page, picked = -1, lastTheme = -1;

    public bool IsAwake => awake;

    static Color Hex(int hex) => new(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, 1f);

    static Material Mat() => new(Graphic.defaultGraphicMaterial);

    static void Remesh(MeshRenderer renderer, Mesh mesh) => renderer.GetComponent<MeshFilter>().sharedMesh = mesh;

    public bool CanPress() => awake && Time.time - lastPress > 0.25f;

    public void Pressed() => lastPress = Time.time;

    public void Play(int kind, bool leftHand) => Sfx.Play(speaker, kind, true, leftHand);

    public string Name(GorillaPlayerScoreboardLine row) =>
        KIDManager.HasPermissionToUseFeature(EKIDFeatures.Custom_Nametags) ? row.playerNameVisible : row.linePlayer.DefaultName;

    public Row Picked() => byActor.TryGetValue(picked, out var row) && !row.leaving ? row : null;

    public static Board Get(GorillaScoreBoard scoreboard)
    {
        if (scoreboard.TryGetComponent<Board>(out var board)) return board.ready ? board : null;
        board = scoreboard.gameObject.AddComponent<Board>();
        board.vanilla = scoreboard;
        if (!reportLooked)
        {
            reportLooked = true;
            var reports = Resources.FindObjectsOfTypeAll<GorillaMetaReport>();
            if (reports.Length > 0) reportBoard = reports[0].currentScoreboard;
        }
        if (scoreboard != reportBoard) board.Init();
        board.enabled = board.ready;
        return board.ready ? board : null;
    }

    void Init()
    {
        try
        {
            var text = vanilla.boardText;
            left = Plugin.left.Value;
            right = Plugin.right.Value;
            bottom = Plugin.bottom.Value;
            top = Plugin.top.Value;
            fontSize = text.fontSize;
            font = text.font;
            fontMat = text.fontSharedMaterial;
            touchLayer = vanilla.lines[0].muteButton.gameObject.layer;

            root = new GameObject("lbplus").transform;
            root.SetParent(vanilla.transform, false);
            root.localPosition = new Vector3(0f, 0f, -2f);
            bgMat = Mat();
            cardMat = Mat();
            rowMat = Mat();
            altMat = Mat();
            accentMat = Mat();
            tagMat = Mat();
            keyMat = Mat();
            darkMat = Mat();
            dangerMat = Mat();
            lavaMat = Mat();
            lavaMat.color = Hex(0xff5a1f);
            whiteMat = Mat();
            whiteMat.color = Color.white;

            speaker = root.gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.spatialBlend = 1f;
            speaker.volume = 0.45f;
            speaker.rolloffMode = AudioRolloffMode.Linear;
            speaker.minDistance = 1f;
            speaker.maxDistance = 10f;

            listLeft = left + Panel.width + 6f;
            listRight = right - 3f;
            listTop = top - 3f;
            listBottom = bottom + 4f;
            var width = listRight - listLeft;
            slab = Solid(root, bgMat, 0, null, (left + right) / 2f, (bottom + top) / 2f);
            header = Label(root);
            Box(header, listLeft + width / 2f, listTop - 8f, width * 0.97f, 14f, 0.8f, TextAlignmentOptions.MidlineLeft);
            taggedTitle = Label(root);
            runnersTitle = Label(root);
            all.Add(this);
            ready = true;
            Restyle();
        }
        catch (Exception e)
        {
            Plugin.log.LogError($"board setup failed, staying vanilla: {e}");
            if (root != null) root.gameObject.SetActive(false);
        }
    }

    public MeshRenderer Solid(Transform parent, Material mat, int order, Mesh mesh, float x = 0f, float y = 0f, float z = 0f)
    {
        var go = new GameObject("q");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, y, z);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    public TextMeshPro Label(Transform parent)
    {
        var go = new GameObject("t");
        go.transform.SetParent(parent, false);
        var label = go.AddComponent<TextMeshPro>();
        label.font = font;
        label.fontSharedMaterial = fontMat;
        label.richText = true;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.enableAutoSizing = true;
        label.fontSizeMin = fontSize * 0.2f;
        label.sortingOrder = 8;
        label.color = Color.white;
        return label;
    }

    public void Box(
        TextMeshPro label, float x, float y, float w, float h, float size, TextAlignmentOptions align, float z = float.NaN)
    {
        label.rectTransform.sizeDelta = new Vector2(w, h);
        label.transform.localPosition = new Vector3(x, y, float.IsNaN(z) ? textZ : z);
        label.fontSizeMax = fontSize * size;
        label.alignment = align;
    }

    public Btn MakeBtn(Transform parent, float x, float y, float w, float h, Action<bool> onPress)
    {
        var go = new GameObject("b") { layer = touchLayer };
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, y, keyZ);
        var seed = Mathf.RoundToInt(x * 3f + y * 7f) & 63;
        var lean = look.tilt * 0.6f;
        var roll = Mathf.Sin(seed * 1.7f) * look.tilt;
        go.transform.localRotation = Quaternion.Euler(Mathf.Sin(seed * 2.3f) * lean, Mathf.Sin(seed * 3.1f) * lean, roll);
        var depth = Mathf.Clamp(h * 0.14f, 1.2f, 2f);
        var body = new GameObject("v").transform;
        body.SetParent(go.transform, false);
        var touch = go.AddComponent<BoxCollider>();
        touch.isTrigger = true;
        touch.center = new Vector3(0f, 0f, -depth);
        touch.size = new Vector3(w, h, line * 0.6f);
        var btn = go.AddComponent<Btn>();
        btn.board = this;
        btn.body = body;
        btn.sink = depth * 0.7f;
        btn.onPress = onPress;
        btn.enabled = false;
        btn.face = Solid(body, keyMat, 5, Skin.Slab(w, h, depth, Mathf.Min(1f, h * 0.12f), 0f, look.wob * 0.25f, seed));
        btn.label = Label(body);
        Box(btn.label, 0f, 0f, w * 0.88f, h * 0.76f, h / line * 0.6f, TextAlignmentOptions.Center, -depth - 0.35f);
        return btn;
    }

    public void Restyle()
    {
        if (!ready) return;
        var index = Mathf.Clamp(Plugin.theme.Value, 0, pals.Length - 1);
        pal = pals[index];
        look = Skin.looks[index];
        bgMat.color = Hex(pal.bg);
        cardMat.color = Hex(pal.card);
        rowMat.color = Hex(pal.row);
        altMat.color = Hex(pal.alt);
        accentMat.color = Hex(pal.accent);
        tagMat.color = Hex(pal.tag);
        keyMat.color = Hex(pal.key);
        darkMat.color = Hex(pal.dark);
        dangerMat.color = Hex(pal.danger);
        if (index != lastTheme) Rebuild(index);
        page = 0;
        foreach (var row in rows)
        {
            row.text = null;
            if (!Plugin.rigs.Value) row.statue.Kill();
        }
        if (!Plugin.rigs.Value) panel.statue.Kill();
        next.Text($"<color={pal.ink}>NEXT 10");
        Layout();
        panel.Refresh();
        if (awake) Sfx.Play(speaker, Synth.swoosh);
    }

    void Rebuild(int index)
    {
        lastTheme = index;
        var rowTex = Skin.Tex(look.row);
        var plainTex = Skin.Tex(Skin.plain);
        bgMat.mainTexture = Skin.Tex(look.board);
        foreach (var mat in new[] { cardMat, rowMat, altMat, tagMat }) mat.mainTexture = rowTex;
        foreach (var mat in new[] { keyMat, dangerMat, accentMat, darkMat, whiteMat, lavaMat }) mat.mainTexture = plainTex;
        Remesh(slab, Skin.Slab(right - left, top - bottom, 2f, 1.6f, look.jit, look.wob, 1));

        foreach (var stud in studs) Destroy(stud);
        studs.Clear();
        var studCount = look.studs ? 4 : 0;
        for (var i = 0; i < studCount; i++)
        {
            var mesh = Skin.Slab(3.4f, 3.4f, 0.9f, 0.9f, 0f, 0f, i);
            float x = i % 2 == 0 ? left + 5f : right - 5f, y = i < 2 ? top - 5f : bottom + 5f;
            studs.Add(Solid(root, darkMat, 1, mesh, x, y, -2.3f).gameObject);
        }

        if (next != null) Destroy(next.gameObject);
        keyZ = rowZ;
        next = MakeBtn(root, listLeft + 16f, listBottom + 6f, 30f, 11f, NextPage);
        next.Face(accentMat);
        if (panel != null) panel.Destroy();
        panel = new Panel(this);
        foreach (var row in rows) row.w = -1f;
    }

    void NextPage(bool leftHand)
    {
        page++;
        nextPage = Time.time + 7f;
        Play(Synth.tap, leftHand);
        Layout();
    }

    public void Sync()
    {
        try
        {
            var stepAside = vanilla.roomControlsActive || vanilla.weatherControlsActive;
            root.gameObject.SetActive(!stepAside);
            if (stepAside) return;
            vanilla.boardText.text = "";
            vanilla.buttonText.text = "";
            vanilla.weatherControlsText.text = "";
            vanilla.linesParent.transform.localScale = Vector3.one * 0.0001f;
            if (vanilla.roomControlsToggle.activeSelf) vanilla.roomControlsToggle.SetActive(false);

            stamp++;
            order.Clear();
            var moved = false;
            foreach (var row in vanilla.lines)
            {
                if (!row.gameObject.activeInHierarchy || row.linePlayer == null || !row.linePlayer.InRoom) continue;
                moved |= Track(row);
            }
            foreach (var row in rows)
            {
                if (row.stamp == stamp || row.leaving) continue;
                row.leaving = true;
                moved = true;
                if (row.actor == picked && !panel.busy) Close();
            }
            if (moved && awake) Sfx.Play(speaker, Synth.swoosh);
            Layout();
            panel.Refresh();
        }
        catch (Exception e)
        {
            Plugin.log.LogError($"board sync failed, going back to vanilla: {e}");
            ready = false;
            enabled = false;
            root.gameObject.SetActive(false);
        }
    }

    bool Track(GorillaPlayerScoreboardLine source)
    {
        var actor = source.linePlayer.ActorNumber;
        var isNew = !byActor.TryGetValue(actor, out var row) || row.leaving;
        if (isNew) row = AddRow(actor);
        if (row.line != source) row.text = null;
        row.line = source;
        row.index = order.Count;
        row.stamp = stamp;
        row.me = source.linePlayer == NetworkSystem.Instance.LocalPlayer;
        order.Add(row);
        return isNew;
    }

    Row AddRow(int actor)
    {
        var go = new GameObject("r") { layer = touchLayer };
        var row = new Row { actor = actor, root = go.transform, body = new GameObject("v").transform };
        row.root.SetParent(root, false);
        row.body.SetParent(row.root, false);
        row.frame = Solid(row.body, accentMat, 2, null);
        row.plank = Solid(row.body, rowMat, 3, null);
        row.dot = Solid(row.body, accentMat, 6, null);
        row.dot.enabled = false;
        row.label = Label(row.body);
        row.touch = go.AddComponent<BoxCollider>();
        row.touch.isTrigger = true;
        var btn = go.AddComponent<Btn>();
        btn.board = this;
        btn.body = row.body;
        btn.enabled = false;
        btn.onPress = leftHand => Select(actor, leftHand);
        rows.Add(row);
        byActor[actor] = row;
        return row;
    }

    void RemoveRow(Row row)
    {
        row.statue.Kill();
        Destroy(row.root.gameObject);
        rows.Remove(row);
        if (byActor.TryGetValue(row.actor, out var current) && current == row) byActor.Remove(row.actor);
    }

    Material PlankMat(Row row)
    {
        if (row.tagged) return tagMat;
        return (row.index & 1) == 1 ? altMat : rowMat;
    }

    void Outline(Row row)
    {
        row.frame.enabled = row.me || row.actor == picked;
        row.frame.sharedMaterial = row.actor == picked ? whiteMat : accentMat;
    }

    void Slot(Row row, float x, float y, float w, float h, bool tile, bool hide)
    {
        row.target = new Vector3(x, y, rowZ);
        row.hidden = hide;
        if (row.w != w || row.h != h || row.tile != tile)
        {
            row.w = w;
            row.h = h;
            row.tile = tile;
            Shape(row);
        }
        if (row.fresh)
        {
            row.fresh = false;
            row.pos = row.target + new Vector3(-w * 0.3f, 0f, 0f);
            row.appear = 0f;
        }
        float touchLeft = x - w / 2f, touchRight = Mathf.Min(x + w / 2f, 5f);
        row.touch.enabled = !hide && touchRight - touchLeft > 6f;
        if (row.touch.enabled)
        {
            row.touch.center = new Vector3((touchLeft + touchRight) / 2f - x, 0f, 0f);
            row.touch.size = new Vector3(touchRight - touchLeft, h, line * 0.5f);
        }
        row.plank.sharedMaterial = row.flash > 0f ? whiteMat : PlankMat(row);
        Outline(row);
    }

    void Shape(Row row)
    {
        float w = row.w, h = row.h, dotSize = row.tile ? 3.4f : 3f;
        Remesh(row.frame, Skin.Round(w + 0.8f, h + 0.8f, 1.2f));
        row.frame.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        Remesh(row.plank, Skin.Slab(w, h, 1.2f, Mathf.Min(0.8f, h * 0.1f), look.jit * 0.45f, look.wob * 0.45f, row.actor & 3));
        row.body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(row.actor * 2.7f) * look.tilt * (row.tile ? 0.9f : 0.3f));
        if (row.tile)
        {
            var strip = h * 0.3f;
            row.rigX = 0f;
            row.rigY = strip * 0.5f;
            row.rigW = w * 0.84f;
            row.rigH = h - strip - 2.5f;
            var size = Mathf.Min(strip / line * 0.7f, w * 0.9f / (12f * charWidth));
            Box(row.label, 0f, -h / 2f + strip / 2f + 1f, w * 0.9f, strip, size, TextAlignmentOptions.Center, -1.6f);
            row.dot.transform.localPosition = new Vector3(w / 2f - 4f, h / 2f - 4f, -1.6f);
        }
        else
        {
            float rig = h * 0.94f, textLeft = -w / 2f + rig * 1.15f, textRight = w / 2f - 7f;
            row.rigX = -w / 2f + rig * 0.58f;
            row.rigY = 0f;
            row.rigW = rig;
            row.rigH = rig;
            var size = Mathf.Min(h / line * 0.62f, (textRight - textLeft) / (15f * charWidth));
            var center = (textLeft + textRight) / 2f;
            Box(row.label, center, 0f, textRight - textLeft, h * 0.86f, size, TextAlignmentOptions.MidlineLeft, -1.6f);
            row.dot.transform.localPosition = new Vector3(w / 2f - 4f, 0f, -1.6f);
        }
        Remesh(row.dot, Skin.Round(dotSize, dotSize, dotSize / 2f));
        row.dot.transform.localScale = Vector3.one;
        row.statue.Fit(new Vector3(row.rigX, row.rigY, -1.4f), row.rigW, row.rigH);
        row.text = null;
    }

    void Layout()
    {
        int count = order.Count, layout = Mathf.Clamp(Plugin.layout.Value, 0, 2), taggedCount = 0;
        bool grouped = Plugin.tagView.Value, zones = grouped && layout != 2, paged = layout == 2 && count > 10;
        float width = listRight - listLeft, listY = listTop - 16f, height = listY - listBottom, gap = 1.6f;
        flow.Clear();
        foreach (var row in order)
        {
            if (!grouped || !row.tagged) continue;
            flow.Add(row);
            taggedCount++;
        }
        foreach (var row in order)
            if (!grouped || !row.tagged) flow.Add(row);
        taggedTitle.gameObject.SetActive(zones);
        runnersTitle.gameObject.SetActive(zones);
        next.Show(paged);

        var cols = 1;
        if (layout == 1) cols = 5;
        else if (layout == 0 && count > 10) cols = 2;
        float colWidth = (width - gap * (cols - 1)) / cols, colX = listLeft + colWidth / 2f;
        if (layout == 2)
        {
            page %= Mathf.Max(1, (count + 9) / 10);
            var rowHeight = (height - (paged ? 14f : 0f)) / 10f;
            for (var i = 0; i < count; i++)
                Slot(flow[i], colX, listY - rowHeight / 2f - i % 10 * rowHeight, width, rowHeight - gap, false, i / 10 != page);
        }
        else if (zones) LayoutZones(count, taggedCount, cols, colWidth, colX, listY, height, gap);
        else if (layout == 0)
        {
            var rowHeight = height / 10f;
            for (var i = 0; i < count; i++)
            {
                var x = colX + Mathf.Min(i / 10, cols - 1) * (colWidth + gap);
                Slot(flow[i], x, listY - rowHeight / 2f - i % 10 * rowHeight, colWidth, rowHeight - gap, false, false);
            }
        }
        else
        {
            var rowHeight = Mathf.Min(colWidth * 1.6f, height / Mathf.Max(1, (count + cols - 1) / cols));
            for (var i = 0; i < count; i++)
            {
                var y = listY - rowHeight / 2f - i / cols * rowHeight;
                Slot(flow[i], colX + i % cols * (colWidth + gap), y, colWidth, rowHeight - gap, true, false);
            }
        }

        var first = vanilla.GetBeginningString();
        var cut = first.IndexOf('\n');
        if (cut > 0) first = first.Substring(0, cut);
        first = $"<color={pal.fg}>{first}</color>";
        if (header.text != first) header.text = first;
    }

    void LayoutZones(int count, int taggedCount, int cols, float colWidth, float colX, float listY, float height, float gap)
    {
        var taggedRows = Mathf.Max(1, (taggedCount + cols - 1) / cols);
        var runnerRows = Mathf.Max(1, (count - taggedCount + cols - 1) / cols);
        float titleHeight = 11f, width = listRight - listLeft, centerX = listLeft + width / 2f;
        var wanted = cols == 5 ? colWidth * 1.6f : height / 10f;
        var rowHeight = Mathf.Min(wanted, (height - titleHeight * 2f) / (taggedRows + runnerRows));
        var runnersY = listY - titleHeight - taggedRows * rowHeight;
        var align = TextAlignmentOptions.MidlineLeft;
        Box(taggedTitle, centerX, listY - titleHeight / 2f, width * 0.97f, titleHeight, 0.62f, align);
        Box(runnersTitle, centerX, runnersY - titleHeight / 2f, width * 0.97f, titleHeight, 0.62f, align);
        taggedTitle.text = $"<color=#ff6a2b>TAGGED {taggedCount}</color>";
        runnersTitle.text = $"<color={pal.accentHex}>RUNNERS {count - taggedCount}</color>";
        for (var i = 0; i < count; i++)
        {
            var isTagged = i < taggedCount;
            var slot = isTagged ? i : i - taggedCount;
            var y = (isTagged ? listY : runnersY) - titleHeight - rowHeight / 2f - slot / cols * rowHeight;
            Slot(flow[i], colX + slot % cols * (colWidth + gap), y, colWidth, rowHeight - gap, cols == 5, false);
        }
    }

    void SetText(Row row)
    {
        var player = row.line.linePlayer;
        var rig = row.line.playerVRRig;
        var sub = SubscriptionManager.GetSubscriptionDetails(player);
        var nameColor = row.muted ? pal.dim : pal.fg;
        if (!row.muted && sub.active && sub.tier > 0) nameColor = pal.gold;
        builder.Clear();
        if (!row.tile)
            builder.Append("<color=").Append(pal.dim).Append('>').Append(row.index + 1 < 10 ? "0" : "")
                .Append(row.index + 1).Append("</color> ");
        builder.Append("<color=").Append(nameColor).Append('>').Append(Name(row.line)).Append("</color>");
        if (row.me) builder.Append(" <size=60%><color=").Append(pal.accentHex).Append(">YOU</color></size>");
        if (Plugin.showHost.Value && player.IsMasterClient)
            builder.Append(" <size=60%><color=").Append(pal.gold).Append(">HOST</color></size>");
        if (row.line.reportedCheating || row.line.reportedHateSpeech || row.line.reportedToxicity)
            builder.Append(" <size=60%><color=#ff6a2b>REPORTED</color></size>");
        if (rig != null && (Plugin.showHz.Value || Plugin.showMs.Value))
        {
            builder.Append(row.tile ? "\n<size=60%><color=" : " <size=65%><color=").Append(pal.dim).Append('>');
            if (Plugin.showHz.Value) builder.Append(Net.Hz(rig)).Append("HZ ");
            var ms = Net.Ms(rig);
            if (Plugin.showMs.Value && ms >= 0) builder.Append(ms).Append("MS");
            builder.Append("</color></size>");
        }
        var text = builder.ToString();
        if (text == row.text) return;
        row.text = text;
        row.label.text = text;
    }

    public void Select(int actor, bool leftHand)
    {
        if (panel.busy) return;
        picked = picked == actor ? -1 : actor;
        Play(picked >= 0 ? Synth.pluck : Synth.down, leftHand);
        foreach (var row in rows) Outline(row);
        panel.Pick();
    }

    public void Close()
    {
        picked = -1;
        foreach (var row in rows) Outline(row);
        panel.Pick();
    }

    public void Step(int dir, bool leftHand)
    {
        if (order.Count == 0) return;
        var at = dir > 0 ? -1 : 0;
        if (byActor.TryGetValue(picked, out var current)) at = order.IndexOf(current);
        var target = order[((at + dir) % order.Count + order.Count) % order.Count];
        if (target.actor == picked) return;
        Select(target.actor, leftHand);
        var slot = flow.IndexOf(target);
        if (Plugin.layout.Value != 2 || slot < 0 || page == slot / 10) return;
        page = slot / 10;
        Layout();
    }

    static float Overshoot(float t)
    {
        t = Mathf.Clamp01(t) - 1f;
        return 1f + 2.70158f * t * t * t + 1.70158f * t * t;
    }

    void Wake(bool near)
    {
        awake = near;
        foreach (var row in rows)
        {
            if (near) row.appear = -row.index * 0.06f;
            else row.statue.Kill();
        }
        if (near)
        {
            Sfx.Play(speaker, Synth.rise);
            return;
        }
        panel.statue.Kill();
        if (!panel.busy) Close();
    }

    void Update()
    {
        float now = Time.time, dt = Mathf.Min(Time.deltaTime, 0.033f);
        if (!root.gameObject.activeSelf) return;
        if (!NetworkSystem.Instance.InRoom)
        {
            root.gameObject.SetActive(false);
            return;
        }
        if (now > nextWake)
        {
            nextWake = now + 0.3f;
            var center = vanilla.transform.TransformPoint(new Vector3((left + right) / 2f, (bottom + top) / 2f, 0f));
            var near = (GorillaTagger.Instance.mainCamera.transform.position - center).sqrMagnitude < 64f;
            if (near != awake) Wake(near);
        }
        if (!awake) return;

        for (var i = rows.Count - 1; i >= 0; i--) Animate(rows[i], now, dt);
        panel.Tick(now);
        if (Plugin.layout.Value == 2 && order.Count > 10 && now > nextPage)
        {
            nextPage = now + 7f;
            page++;
            Layout();
        }
        if (now <= nextSlow) return;
        nextSlow = now + 0.25f;
        Slow(now);
    }

    void Animate(Row row, float now, float dt)
    {
        var shrinking = row.leaving || row.hidden;
        row.appear = Mathf.MoveTowards(row.appear, shrinking ? 0f : 1f, dt / 0.35f);
        if (row.leaving && row.appear <= 0f)
        {
            RemoveRow(row);
            return;
        }
        row.vel += ((row.target - row.pos) * 220f - row.vel * 22f) * dt;
        row.pos += row.vel * dt;
        row.root.localPosition = row.pos;
        var scale = shrinking ? Mathf.Clamp01(row.appear) : Overshoot(row.appear);
        row.root.localScale = Vector3.one * Mathf.Max(scale, 0.0001f);
        if (row.flash > 0f)
        {
            row.flash -= dt / 0.5f;
            if (row.flash <= 0f) row.plank.sharedMaterial = PlankMat(row);
        }
        if (row.speaking) row.dot.transform.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(now * 14f));
    }

    void Slow(float now)
    {
        bool relayout = false, textTick = now > nextText && (Plugin.showHz.Value || Plugin.showMs.Value);
        if (now > nextText) nextText = now + 1f;
        var built = 0;
        foreach (var row in rows)
        {
            if (row.leaving || row.line == null || row.line.linePlayer == null) continue;
            var rig = row.line.playerVRRig;
            relayout |= Tagged(row, rig != null && rig.setMatIndex > 0);
            Voice(row, rig, now);
            if (Plugin.rigs.Value && rig != null) built += Copy(row, rig, now, built < 2);
            if (row.text == null || textTick) SetText(row);
        }
        if (relayout) Layout();
        panel.Refresh();
    }

    bool Tagged(Row row, bool tagged)
    {
        if (tagged != row.tagged)
        {
            row.tagged = tagged;
            if (row.primed)
            {
                row.flash = 1f;
                Sfx.Play(speaker, tagged ? Synth.thud : Synth.rise);
            }
            row.plank.sharedMaterial = row.primed ? whiteMat : PlankMat(row);
            row.primed = true;
            return Plugin.tagView.Value;
        }
        row.primed = true;
        return false;
    }

    void Voice(Row row, VRRig rig, float now)
    {
        var mute = row.line.muteButton;
        bool speaking = row.line.speakerIcon.enabled, muted = mute.isOn || mute.isAutoOn;
        if (speaking != row.speaking || muted != row.muted)
        {
            row.speaking = speaking;
            row.muted = muted;
            row.dot.sharedMaterial = muted ? lavaMat : accentMat;
            row.dot.enabled = speaking || muted;
            row.dot.transform.localScale = Vector3.one;
            row.text = null;
        }
        var id = row.line.linePlayer.UserId;
        var hasVoice = rig != null && rig.voiceAudio != null;
        if (hasVoice && Mutes.volumes.TryGetValue(id, out var volume)) rig.voiceAudio.volume = volume;
        if (mute.isOn && Mutes.timed.TryGetValue(id, out var until) && now > until) Mutes.Toggle(row.line);
    }

    int Copy(Row row, VRRig rig, float now, bool mayBuild)
    {
        if (!row.statue.Stale())
        {
            row.statue.SyncMats();
            return 0;
        }
        if (row.statueDue == 0f)
        {
            row.statueDue = now + 0.8f;
            return 0;
        }
        if (now < row.statueDue || !mayBuild) return 0;
        row.statueDue = 0f;
        try
        {
            row.statue.Build(rig, row.body);
            row.statue.Fit(new Vector3(row.rigX, row.rigY, -1.4f), row.rigW, row.rigH);
        }
        catch (Exception e)
        {
            Plugin.log.LogError($"rig copy failed: {e}");
            row.statueDue = float.MaxValue;
        }
        return 1;
    }

    void OnDestroy()
    {
        all.Remove(this);
        var mats = new[] { bgMat, cardMat, rowMat, altMat, accentMat, tagMat, keyMat, darkMat, dangerMat, lavaMat, whiteMat };
        foreach (var mat in mats)
            Destroy(mat);
    }
}

public static class Sfx
{
    static readonly float[] buzz = { 0.1f, 0.3f, 0f, 0f, 0.55f, 0f, 0.2f, 0.15f, 0.2f, 0.25f, 0.3f, 0.35f };
    static readonly float[] buzzTime = { 0.02f, 0.04f, 0f, 0f, 0.09f, 0f, 0.03f, 0.03f, 0.03f, 0.03f, 0.03f, 0.04f };
    static AudioClip[] clips;
    static float last;

    static void Build()
    {
        clips = new AudioClip[Synth.count];
        for (var i = 0; i < clips.Length; i++)
        {
            var samples = Synth.Make(i);
            clips[i] = AudioClip.Create($"lb{i}", samples.Length, 1, Synth.rate, false);
            clips[i].SetData(samples, 0);
        }
    }

    public static void Play(AudioSource source, int kind, bool mine = false, bool left = false)
    {
        if (clips == null) Build();
        if (Time.unscaledTime - last < 0.08f) return;
        last = Time.unscaledTime;
        source.PlayOneShot(clips[kind]);
        if (mine && buzz[kind] > 0f) GorillaTagger.Instance.StartVibration(left, buzz[kind], buzzTime[kind]);
    }
}
