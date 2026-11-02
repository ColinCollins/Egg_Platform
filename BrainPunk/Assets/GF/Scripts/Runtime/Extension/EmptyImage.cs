using UnityEngine.UI;

namespace SPGame
{
    public class EmptyImage : Image
    {
        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            toFill.Clear();
        }
    }
}