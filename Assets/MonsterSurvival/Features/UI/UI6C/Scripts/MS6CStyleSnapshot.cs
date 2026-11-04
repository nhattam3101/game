using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Serialization-only backup. Nothing in here drives gameplay.
public sealed class MS6CStyleSnapshot : MonoBehaviour
{
    [Serializable] public class ImageState
    {
        public Image image;
        public Sprite sprite;
        public Color color;
        public Image.Type type;
        public bool raycastTarget;
    }
    [Serializable] public class TextState
    {
        public TMP_Text text;
        public Color color;
        public float fontSize;
    }
    [Serializable] public class ButtonState
    {
        public Button button;
        public Selectable.Transition transition;
        public ColorBlock colors;
    }
    [Serializable] public class RectState
    {
        public RectTransform rect;
        public Vector2 anchorMin, anchorMax, anchoredPosition, sizeDelta, pivot;
    }
    [Serializable] public class ActiveState
    {
        public GameObject gameObject;
        public bool active;
    }
    public List<ImageState> images = new List<ImageState>();
    public List<TextState> texts = new List<TextState>();
    public List<ButtonState> buttons = new List<ButtonState>();
    public List<RectState> rects = new List<RectState>();
    public List<ActiveState> activeStates = new List<ActiveState>();
    public List<GameObject> generatedObjects = new List<GameObject>();
    public List<Component> generatedComponents = new List<Component>();
}
