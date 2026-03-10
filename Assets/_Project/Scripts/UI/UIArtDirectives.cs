using UnityEngine;

[DisallowMultipleComponent]
public class UIArtDirectives : MonoBehaviour
{
    [Header("Assigned Art")]
    [SerializeField] private bool preserveAssignedSprites = true;
    [SerializeField] private bool preserveAssignedColors = true;

    [Header("Procedural Styling")]
    [SerializeField] private bool preserveTextStyling;
    [SerializeField] private bool preserveButtonTransitions;
    [SerializeField] private bool allowGeneratedDecor = true;
    [SerializeField] private bool allowProceduralLayout = true;

    public bool PreserveAssignedSprites => preserveAssignedSprites;
    public bool PreserveAssignedColors => preserveAssignedColors;
    public bool PreserveTextStyling => preserveTextStyling;
    public bool PreserveButtonTransitions => preserveButtonTransitions;
    public bool AllowGeneratedDecor => allowGeneratedDecor;
    public bool AllowProceduralLayout => allowProceduralLayout;
}
