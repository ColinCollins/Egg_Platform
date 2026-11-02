using UnityEditor;

namespace GF.Editor
{
    public abstract class InspectorBase: UnityEditor.Editor
    {
        private bool _isCompiling = false;
        public override void OnInspectorGUI()
        {
            if (_isCompiling && !EditorApplication.isCompiling)
            {
                _isCompiling = false;
                OnCompileComplete();
            }
            else if (!_isCompiling && EditorApplication.isCompiling)
            {
                _isCompiling = true;
                OnCompileStart();
            }
        }
        protected abstract void OnCompileStart();
        protected abstract void OnCompileComplete();
    }
}