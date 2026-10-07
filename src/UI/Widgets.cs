using System;
using TMPro;
using UnityEngine;

namespace LeaderboardPlus;

public class Btn : MonoBehaviour
{
    public Board board;
    public Action<bool> onPress;
    public Transform body;
    public MeshRenderer face;
    public TextMeshPro label;
    public float sink;
    string shown;
    float spring = -1f;

    void OnTriggerEnter(Collider other)
    {
        if (onPress == null || !board.CanPress()) return;
        var hand = other.GetComponent<GorillaTriggerColliderHandIndicator>();
        if (hand == null) return;
        board.Pressed();
        spring = 0f;
        enabled = true;
        onPress(hand.isLeftHand);
    }

    void Update()
    {
        if (spring < 0f)
        {
            enabled = false;
            return;
        }
        spring += Time.deltaTime / 0.5f;
        var bounce = spring >= 1f ? 0f : Mathf.Exp(-5f * spring) * Mathf.Cos(9f * spring);
        if (sink > 0f) body.localPosition = new Vector3(0f, 0f, sink * bounce);
        else body.localScale = Vector3.one * (1f - 0.1f * bounce);
        if (spring >= 1f) spring = -1f;
    }

    public void Show(bool on)
    {
        if (gameObject.activeSelf != on) gameObject.SetActive(on);
    }

    public void Text(string text)
    {
        if (text == shown) return;
        shown = text;
        label.text = text;
    }

    public void Face(Material mat)
    {
        if (face.sharedMaterial != mat) face.sharedMaterial = mat;
    }
}

public class Slider : MonoBehaviour
{
    public Board board;
    public Action<float, bool> onMove;
    public float width;
    float last = -1f;

    void OnTriggerStay(Collider other)
    {
        if (onMove == null || !board.IsAwake) return;
        var hand = other.GetComponent<GorillaTriggerColliderHandIndicator>();
        if (hand == null) return;
        var value = Mathf.Clamp01(transform.InverseTransformPoint(other.transform.position).x / width + 0.5f);
        if (Mathf.Abs(value - last) < 0.004f) return;
        last = value;
        onMove(value, hand.isLeftHand);
    }
}
