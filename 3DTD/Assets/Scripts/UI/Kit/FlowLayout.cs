using UnityEngine;
using UnityEngine.UI;

// Left-to-right layout that wraps children onto new lines (chips). Children keep their preferred size.
public class FlowLayout : LayoutGroup
{
    [SerializeField] private float spacingX = 6f;
    [SerializeField] private float spacingY = 6f;

    private float arrangedHeight;

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        SetLayoutInputForAxis(padding.horizontal, padding.horizontal, -1f, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        arrangedHeight = Arrange(false);
        SetLayoutInputForAxis(arrangedHeight, arrangedHeight, -1f, 1);
    }

    public override void SetLayoutHorizontal()
    {
        Arrange(true);
    }

    public override void SetLayoutVertical()
    {
        Arrange(true);
    }

    private float Arrange(bool apply)
    {
        float width = rectTransform.rect.width - padding.horizontal;
        float x = 0f;
        float y = 0f;
        float lineHeight = 0f;
        for (int i = 0; i < rectChildren.Count; i++)
        {
            RectTransform child = rectChildren[i];
            float childWidth = Mathf.Min(LayoutUtility.GetPreferredWidth(child), width);
            float childHeight = LayoutUtility.GetPreferredHeight(child);
            if (x > 0f && x + childWidth > width)
            {
                x = 0f;
                y += lineHeight + spacingY;
                lineHeight = 0f;
            }
            if (apply)
            {
                SetChildAlongAxis(child, 0, padding.left + x, childWidth);
                SetChildAlongAxis(child, 1, padding.top + y, childHeight);
            }
            x += childWidth + spacingX;
            lineHeight = Mathf.Max(lineHeight, childHeight);
        }
        return padding.vertical + y + lineHeight;
    }
}
