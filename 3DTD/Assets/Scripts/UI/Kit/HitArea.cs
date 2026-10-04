using UnityEngine;
using UnityEngine.UI;

// Invisible raycast target. Grows small controls to the 44 px minimum touch target and lets plates
// catch clicks without drawing anything.
[RequireComponent(typeof(CanvasRenderer))]
public class HitArea : Graphic
{
    public override void SetMaterialDirty() { }
    public override void SetVerticesDirty() { }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
    }
}
