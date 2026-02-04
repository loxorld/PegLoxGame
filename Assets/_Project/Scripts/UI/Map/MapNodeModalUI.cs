using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MapNodeModalUI : MonoBehaviour, IMapNodeModalView
{
    public struct Option
    {
        public string Label;
        public Action Callback;

        public Option(string label, Action callback)
        {
            Label = label;
            Callback = callback;
        }
    }

    private static MapNodeModalUI instance;

    private Text titleText;
    private Text bodyText;
    private readonly List<Button> buttons = new();

    public static MapNodeModalUI Instance => instance;

    public static void Show(string title, string body, params Option[] options)
    {
        if (instance == null || instance.Equals(null))
            instance = CreateModal();

        instance.gameObject.SetActive(true);
        instance.titleText.text = title ?? string.Empty;
        instance.bodyText.text = body ?? string.Empty;

        instance.SetupButtons(options);
    }

    public void ShowEvent(string title, string description, IReadOnlyList<MapNodeModalOption> options)
    {
        Show(title, description, ConvertOptions(options));
    }

    public void ShowShop(string title, string description, IReadOnlyList<MapNodeModalOption> options)
    {
        Show(title, description, ConvertOptions(options));
    }

    public void ShowGeneric(string title, string description, IReadOnlyList<MapNodeModalOption> options)
    {
        Show(title, description, ConvertOptions(options));
    }
    private static MapNodeModalUI CreateModal()
    {
        var root = new GameObject("MapNodeModalUI");
        var modal = root.AddComponent<MapNodeModalUI>();
        modal.BuildUI(root);
        return modal;
    }

    private void BuildUI(GameObject root)
    {
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        root.AddComponent<GraphicRaycaster>();

        var panelObj = CreateUIObject("Panel", root.transform);
        var panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.75f);

        var panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(900f, 520f);
        panelRect.anchoredPosition = Vector2.zero;

        var titleObj = CreateUIObject("Title", panelObj.transform);
        titleText = titleObj.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 40;
        titleText.alignment = TextAnchor.UpperCenter;
        titleText.color = Color.white;

        var titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0f, 80f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);

        var bodyObj = CreateUIObject("Body", panelObj.transform);
        bodyText = bodyObj.AddComponent<Text>();
        bodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bodyText.fontSize = 28;
        bodyText.alignment = TextAnchor.UpperLeft;
        bodyText.color = Color.white;
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Overflow;

        var bodyRect = bodyObj.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0.08f, 0.2f);
        bodyRect.anchorMax = new Vector2(0.92f, 0.78f);
        bodyRect.pivot = new Vector2(0.5f, 0.5f);
        bodyRect.anchoredPosition = Vector2.zero;

        var buttonsContainer = CreateUIObject("Buttons", panelObj.transform);
        var buttonsRect = buttonsContainer.GetComponent<RectTransform>();
        buttonsRect.anchorMin = new Vector2(0.1f, 0.05f);
        buttonsRect.anchorMax = new Vector2(0.9f, 0.2f);
        buttonsRect.pivot = new Vector2(0.5f, 0.5f);
        buttonsRect.anchoredPosition = Vector2.zero;

        var layout = buttonsContainer.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 24f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;
    }

    private void SetupButtons(Option[] options)
    {
        foreach (var button in buttons)
            Destroy(button.gameObject);
        buttons.Clear();

        if (options == null || options.Length == 0)
            options = new[] { new Option("Continuar", Close) };

        var buttonsContainer = transform.Find("Panel/Buttons");
        if (buttonsContainer == null)
            return;

        foreach (var option in options)
        {
            var buttonObj = CreateUIObject("Button", buttonsContainer);
            var buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

            var button = buttonObj.AddComponent<Button>();
            button.onClick.AddListener(() =>
            {
                option.Callback?.Invoke();
                Close();
            });

            var textObj = CreateUIObject("Label", buttonObj.transform);
            var text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 26;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = option.Label;

            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(240f, 60f);

            buttons.Add(button);
        }
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private void Close()
    {
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
    private static Option[] ConvertOptions(IReadOnlyList<MapNodeModalOption> options)
    {
        if (options == null || options.Count == 0)
            return Array.Empty<Option>();

        var converted = new Option[options.Count];
        for (int i = 0; i < options.Count; i++)
            converted[i] = new Option(options[i].Label, options[i].OnSelect);
        return converted;
    }
}
