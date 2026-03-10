using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class UIArtUtility
{
    private static Sprite builtinPanelSprite;

    public static Sprite BuiltinPanelSprite =>
        builtinPanelSprite != null
            ? builtinPanelSprite
            : builtinPanelSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");

    public static bool HasCustomSprite(Image image)
    {
        return image != null
            && image.sprite != null
            && image.sprite != BuiltinPanelSprite;
    }

    public static bool ShouldPreserveSprite(Image image)
    {
        if (image == null || image.sprite == null)
            return false;

        UIArtDirectives directives = GetDirectives(image);
        if (directives != null)
            return directives.PreserveAssignedSprites;

        return HasCustomSprite(image);
    }

    public static bool ShouldPreserveColor(Graphic graphic)
    {
        if (graphic == null)
            return false;

        UIArtDirectives directives = GetDirectives(graphic);
        if (directives != null)
            return directives.PreserveAssignedColors;

        return graphic is Image image && HasCustomSprite(image);
    }

    public static bool ShouldPreserveTextStyling(TMP_Text text)
    {
        UIArtDirectives directives = GetDirectives(text);
        return directives != null && directives.PreserveTextStyling;
    }

    public static bool ShouldPreserveButtonTransitions(Button button)
    {
        if (button == null)
            return false;

        UIArtDirectives directives = GetDirectives(button);
        if (directives != null)
            return directives.PreserveButtonTransitions;

        return button.targetGraphic is Image image && HasCustomSprite(image);
    }

    public static bool AllowsGeneratedDecor(Component component)
    {
        UIArtDirectives directives = GetDirectives(component);
        if (directives != null)
            return directives.AllowGeneratedDecor;

        if (component is Button button && button.targetGraphic is Image buttonImage)
            return !HasCustomSprite(buttonImage);

        if (component is Image image)
            return !HasCustomSprite(image);

        if (component is RectTransform rectTransform)
        {
            Image rootImage = rectTransform.GetComponent<Image>();
            if (rootImage != null)
                return !HasCustomSprite(rootImage);
        }

        return true;
    }

    public static bool AllowsProceduralLayout(Component component)
    {
        UIArtDirectives directives = GetDirectives(component);
        return directives == null || directives.AllowProceduralLayout;
    }

    public static void ApplyImageStyle(Image image, Color fallbackColor, bool raycastTarget, Image.Type fallbackType = Image.Type.Sliced, Sprite fallbackSprite = null)
    {
        if (image == null)
            return;

        if (!ShouldPreserveSprite(image))
        {
            image.sprite = fallbackSprite != null ? fallbackSprite : BuiltinPanelSprite;
            image.type = fallbackType;
        }

        if (!ShouldPreserveColor(image))
            image.color = fallbackColor;

        image.raycastTarget = raycastTarget;
    }

    public static void ApplyButtonStyle(
        Button button,
        Color normalColor,
        Color highlightedColor,
        Color pressedColor,
        Color disabledColor,
        bool raycastTarget,
        Image.Type fallbackType = Image.Type.Sliced,
        Sprite fallbackSprite = null,
        float fadeDuration = 0.08f)
    {
        if (button == null)
            return;

        Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
        if (image == null)
            image = button.gameObject.AddComponent<Image>();

        UIArtDirectives directives = GetDirectives(button);
        bool preserveAssignedColors = directives != null && directives.PreserveAssignedColors;
        bool preserveButtonTransitions = directives != null && directives.PreserveButtonTransitions;

        if (!ShouldPreserveSprite(image))
        {
            image.sprite = fallbackSprite != null ? fallbackSprite : BuiltinPanelSprite;
            image.type = fallbackType;
        }

        if (!preserveAssignedColors)
            image.color = normalColor;

        image.raycastTarget = raycastTarget;
        button.targetGraphic = image;

        if (preserveButtonTransitions)
            return;

        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = highlightedColor;
        colors.pressedColor = pressedColor;
        colors.selectedColor = highlightedColor;
        colors.disabledColor = disabledColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = fadeDuration;
        button.colors = colors;
    }

    public static string ResolveDynamicText(TMP_Text text, string plainText, string richText)
    {
        return ShouldPreserveTextStyling(text) ? plainText : richText;
    }

    public static UIArtDirectives GetDirectives(Component component)
    {
        if (component == null)
            return null;

        UIArtDirectives local = component.GetComponent<UIArtDirectives>();
        if (local != null)
            return local;

        return component.GetComponentInParent<UIArtDirectives>(true);
    }
}
