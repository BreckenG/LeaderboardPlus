using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace LeaderboardPlus;

public class Statue
{
    static Dictionary<VRRig, int> versions = new();
    static Dictionary<Transform, int> byRef = new();
    static Dictionary<string, int> byName = new();
    static List<Material> mats = new();
    static StringBuilder note = new(600);
    static MaterialPropertyBlock props;
    static int logged;

    public Transform holder;
    public VRRig rig;
    SkinnedMeshRenderer body;
    Transform[] bones;
    Matrix4x4[] binds;
    List<Renderer> sources = new();
    List<MeshRenderer> copies = new();
    Bounds box;
    int version, lost, skinned;

    public static void Dirty(VRRig rig)
    {
        versions.TryGetValue(rig, out var version);
        versions[rig] = version + 1;
    }

    static SkinnedMeshRenderer Body(VRRig rig)
    {
        var active = rig.bodyRenderer != null ? rig.bodyRenderer.ActiveBody : null;
        return active != null ? active : rig.mainSkin;
    }

    public bool Stale()
    {
        if (holder == null || rig == null) return true;
        versions.TryGetValue(rig, out var current);
        return current != version || Body(rig) != body;
    }

    public void Kill()
    {
        if (holder != null) Object.Destroy(holder.gameObject);
        holder = null;
        sources.Clear();
        copies.Clear();
    }

    bool Skip(Renderer part, string why)
    {
        if (logged < 2) note.Append(" | ").Append(part.name).Append(": ").Append(why);
        return false;
    }

    bool Lose(Renderer part, string why)
    {
        lost++;
        return Skip(part, why);
    }

    static bool Find(Transform bone, out int index) =>
        byRef.TryGetValue(bone, out index) || byName.TryGetValue(bone.name, out index);

    int Near(Vector3 point, float max)
    {
        var best = -1;
        var bestSqr = max * max;
        for (var i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null) continue;
            var sqr = (bones[i].position - point).sqrMagnitude;
            if (sqr >= bestSqr) continue;
            bestSqr = sqr;
            best = i;
        }
        return best;
    }

    bool Shared(Transform[] theirs, int count, out int ours, out int their)
    {
        ours = -1;
        for (their = 0; their < count; their++)
            if (theirs[their] != null && Find(theirs[their], out ours)) return true;
        for (their = 0; their < count; their++)
        {
            if (theirs[their] == null) continue;
            ours = Near(theirs[their].position, 0.03f);
            if (ours >= 0) return true;
        }
        return false;
    }

    bool PlaceSkinned(SkinnedMeshRenderer part, out Mesh mesh, out Matrix4x4 pose)
    {
        mesh = part.sharedMesh;
        pose = Matrix4x4.identity;
        if (mesh == null) return false;
        if (part != body)
        {
            var theirBinds = mesh.bindposes;
            var theirs = part.bones;
            var count = Mathf.Min(theirs.Length, theirBinds.Length);
            if (!Shared(theirs, count, out var ours, out var their) || ours >= binds.Length)
                return Lose(part, "skinned, no shared bone");
            pose = binds[ours].inverse * theirBinds[their];
        }
        skinned++;
        return true;
    }

    bool PlaceSolid(Renderer part, out Mesh mesh, out Matrix4x4 pose)
    {
        mesh = null;
        pose = Matrix4x4.identity;
        if (!(part is MeshRenderer) || part.GetComponent<TMP_Text>() != null) return false;
        var filter = part.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return Skip(part, "no mesh");
        mesh = filter.sharedMesh;
        var anchor = part.transform;
        var bone = -1;
        while (anchor != null && anchor != rig.transform && !Find(anchor, out bone)) anchor = anchor.parent;
        if (anchor == null || anchor == rig.transform)
        {
            bone = Near(part.bounds.center, 1000f);
            if (bone < 0 || bone >= binds.Length) return Lose(part, "no bone near");
            anchor = bones[bone];
        }
        if (logged < 2) note.Append(" | ").Append(part.name).Append(" -> ").Append(bones[bone].name);
        pose = binds[bone].inverse * (anchor.worldToLocalMatrix * part.transform.localToWorldMatrix);
        return true;
    }

    void Copy(Renderer part, Mesh mesh, Matrix4x4 pose, Transform root)
    {
        Vector3 x = pose.GetColumn(0), y = pose.GetColumn(1), z = pose.GetColumn(2);
        if (float.IsNaN(pose.m03) || y.sqrMagnitude < 1e-12f || z.sqrMagnitude < 1e-12f)
        {
            Lose(part, "bad matrix");
            return;
        }
        var go = new GameObject("c");
        go.transform.SetParent(root, false);
        go.transform.localPosition = pose.GetColumn(3);
        go.transform.localRotation = Quaternion.LookRotation(z, y);
        go.transform.localScale = new Vector3(x.magnitude, y.magnitude, z.magnitude);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var copy = go.AddComponent<MeshRenderer>();
        copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sources.Add(part);
        copies.Add(copy);
    }

    public void Build(VRRig target, Transform parent)
    {
        Kill();
        rig = target;
        versions.TryGetValue(rig, out version);
        body = Body(rig);
        props ??= new MaterialPropertyBlock();
        holder = new GameObject("rig").transform;
        holder.SetParent(parent, false);
        holder.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
        if (body == null || body.sharedMesh == null) return;

        var root = new GameObject("m").transform;
        root.SetParent(holder, false);
        root.localRotation = Quaternion.Inverse(rig.transform.rotation) * body.transform.rotation;
        bones = body.bones;
        binds = body.sharedMesh.bindposes;
        byRef.Clear();
        byName.Clear();
        for (var i = 0; i < bones.Length && i < binds.Length; i++)
        {
            if (bones[i] == null) continue;
            byRef[bones[i]] = i;
            byName[bones[i].name] = i;
        }

        lost = 0;
        skinned = 0;
        note.Clear();
        foreach (var part in rig.GetComponentsInChildren<Renderer>(false))
        {
            if (sources.Count >= 48) break;
            if (!part.enabled || part.forceRenderingOff) continue;
            Mesh mesh;
            Matrix4x4 pose;
            var placed = part is SkinnedMeshRenderer skin
                ? PlaceSkinned(skin, out mesh, out pose)
                : PlaceSolid(part, out mesh, out pose);
            if (placed) Copy(part, mesh, pose, root);
        }

        var bounds = body.sharedMesh.bounds;
        box = new Bounds(root.localRotation * bounds.center, Vector3.zero);
        for (var i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
            box.Encapsulate(root.localRotation * (bounds.center + Vector3.Scale(bounds.extents, corner)));
        }
        SyncMats();
        if (logged < 6) Report();
    }

    void Report()
    {
        logged++;
        Plugin.log.LogInfo($"rig copy: {copies.Count} parts ({skinned} skinned), {lost} lost, {bones.Length} bones{note}");
        if (logged != 1) return;
        note.Clear();
        foreach (var bone in bones) note.Append(bone != null ? bone.name : "null").Append(", ");
        var first = bones.Length > 0 && bones[0] != null && bones[0].parent != null ? bones[0].parent.name : "none";
        Plugin.log.LogInfo($"body bones: {note}| parent of first bone: {first}");
    }

    public void Fit(Vector3 center, float w, float h)
    {
        if (holder == null || box.size.y <= 0f) return;
        var scale = Mathf.Min(h / box.size.y, w / Mathf.Max(box.size.x, 0.0001f));
        holder.localScale = Vector3.one * scale;
        var forward = new Vector3(0f, 0f, -box.size.z * scale * 0.5f);
        holder.localPosition = center - holder.localRotation * box.center * scale + forward;
    }

    public void SyncMats()
    {
        if (holder == null) return;
        for (var i = 0; i < sources.Count; i++)
        {
            if (sources[i] == null) continue;
            sources[i].GetSharedMaterials(mats);
            copies[i].SetSharedMaterials(mats);
            sources[i].GetPropertyBlock(props);
            copies[i].SetPropertyBlock(props);
        }
    }
}
