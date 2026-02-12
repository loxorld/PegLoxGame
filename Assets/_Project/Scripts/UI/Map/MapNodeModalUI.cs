using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapNodeModalUI : MonoBehaviour, IMapNodeModalView
{
    public struct Option
    {
        public string Label;
        public Action Callback;
        public bool IsEnabled;

        public Option(string label, Action callback, bool isEnabled)
        {
            Label = label;
            Callback = callback;
            IsEnabled = isEnabled;
        }
    }

    private const string ResourcePath = "MapNodeModalUI";

    private static MapNodeModalUI instance;

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Transform buttonsContainer;
    [SerializeField] private Button buttonTemplate;

    private readonly List<Button> buttons = new();

    public static MapNodeModalUI Instance => instance;

    public static MapNodeModalUI GetOrCreate()
    {
        if (instance == null || instance.Equals(null))
            instance = CreateModal();

        return instance;
    }
    public static void Show(string title, string body, params Option[] options)
    {
        if (instance == null || instance.Equals(null))
            instance = CreateModal();

        if (instance == null)
            return;

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
        var prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[MapNodeModalUI] No se encontr el prefab en Resources/{ResourcePath}.");
            return null;
        }

        var modalInstance = Instantiate(prefab);
        var modal = modalInstance.GetComponent<MapNodeModalUI>();
        if (modal == null)
            Debug.LogError("[MapNodeModalUI] El prefab no tiene el componente MapNodeModalUI.");

        var modalCanvas = modalInstance.GetComponentInChildren<Canvas>(true);
        var rootCanvas = GameObject.Find("MapCanvas") ?? GameObject.Find("UIRoot");
        if (modalCanvas == null && rootCanvas != null)
            modalInstance.transform.SetParent(rootCanvas.transform, false);
        else
            modalInstance.transform.SetParent(null, false);

        modalInstance.transform.localScale = Vector3.one;
        if (modalInstance.transform is RectTransform rectTransform)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
        }
        if (modal == null)
            Debug.LogError("[MapNodeModalUI] El prefab no tiene el componente MapNodeModalUI.");
        return modal;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (buttonTemplate != null)
            buttonTemplate.gameObject.SetActive(false);
    }

    private void SetupButtons(Option[] options)
    {
        foreach (var button in buttons)
            Destroy(button.gameObject);
        buttons.Clear();

        if (options == null || options.Length == 0)
            options = new[] { new Option("Continuar", Close, true) };

        if (buttonsContainer == null || buttonTemplate == null)
        {
            Debug.LogWarning("[MapNodeModalUI] Faltan referencias de UI en el prefab.");
            return;
        }

        foreach (var option in options)
        {
            var button = Instantiate(buttonTemplate, buttonsContainer);
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();
            button.interactable = option.IsEnabled;
            button.onClick.AddListener(() =>
            {
                
                Close();
                option.Callback?.Invoke();
            });

            var text = button.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = option.Label;

            buttons.Add(button);
        }
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
            converted[i] = new Option(options[i].Label, options[i].OnSelect, options[i].IsEnabled);
        return converted;
    }
}
