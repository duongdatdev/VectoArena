using System;
using UnityEngine;

namespace VectoArena.UI.MainMenu
{
    /// <summary>
    /// Controller for the AnimatedBackground, always use this to modify the background in any way.
    /// It is basically a wrapper to change the ScrollingBackground shader
    /// </summary>
    public class AnimatedBackground : MonoBehaviour
    {
        [Serializable]
        public class AnimatedBackgroundColor
        {
            public Color Bottom;
            public Color Middle;
            public Color Top;
            public Color Pattern;
        }

        private static readonly int _trigerredColorChange = Shader.PropertyToID("_TrigerredColorChange");

        private static readonly int _colorTopPID = Shader.PropertyToID("_ColorTop");
        private static readonly int _colorMiddlePID = Shader.PropertyToID("_ColorMiddle");
        private static readonly int _colorBottomPID = Shader.PropertyToID("_ColorBottom");
        private static readonly int _colorPatternPID = Shader.PropertyToID("_ColorPattern");

        private static readonly int _colorTargetTopPID = Shader.PropertyToID("_ColorTopTarget");
        private static readonly int _colorTargetMiddlePID = Shader.PropertyToID("_ColorMiddleTarget");
        private static readonly int _colorTargetBottomPID = Shader.PropertyToID("_ColorBottomTarget");
        private static readonly int _colorTargetPatternPID = Shader.PropertyToID("_ColorPatternTarget");

        [SerializeField] private Renderer _quadRenderer;

        [SerializeField] private AnimatedBackgroundColor _default;
        [SerializeField] private AnimatedBackgroundColor _dimmedColor;

        private AnimatedBackgroundColor _lastColor;

        private void Awake()
        {
            if (_quadRenderer == null)
            {
                _quadRenderer = GetComponent<Renderer>();
            }
            
            if (_default == null)
            {
                _default = new AnimatedBackgroundColor();
                if (_quadRenderer != null && _quadRenderer.material != null)
                {
                    _default.Top = _quadRenderer.material.HasProperty(_colorTopPID) ? _quadRenderer.material.GetColor(_colorTopPID) : Color.blue;
                    _default.Middle = _quadRenderer.material.HasProperty(_colorMiddlePID) ? _quadRenderer.material.GetColor(_colorMiddlePID) : Color.cyan;
                    _default.Bottom = _quadRenderer.material.HasProperty(_colorBottomPID) ? _quadRenderer.material.GetColor(_colorBottomPID) : Color.black;
                    _default.Pattern = _quadRenderer.material.HasProperty(_colorPatternPID) ? _quadRenderer.material.GetColor(_colorPatternPID) : Color.white;
                }
            }
            
            SetDefault();
        }

        public void SetDefault()
        {
            SetColor(_default);
        }

        public void SetDimmed()
        {
            SetColor(_dimmedColor);
        }

        public void SetColor(AnimatedBackgroundColor color, bool animate = false)
        {
            if (_quadRenderer == null || _quadRenderer.material == null) return;

            if (_lastColor != null && animate)
            {
                _quadRenderer.material.SetColor(_colorTopPID, _lastColor.Top);
                _quadRenderer.material.SetColor(_colorMiddlePID, _lastColor.Middle);
                _quadRenderer.material.SetColor(_colorBottomPID, _lastColor.Bottom);
                _quadRenderer.material.SetColor(_colorPatternPID, _lastColor.Pattern);
            }
            else
            {
                _quadRenderer.material.SetColor(_colorTopPID, color.Top);
                _quadRenderer.material.SetColor(_colorMiddlePID, color.Middle);
                _quadRenderer.material.SetColor(_colorBottomPID, color.Bottom);
                _quadRenderer.material.SetColor(_colorPatternPID, color.Pattern);
            }

            _quadRenderer.material.SetFloat(_trigerredColorChange, Time.time);
            _quadRenderer.material.SetColor(_colorTargetTopPID, color.Top);
            _quadRenderer.material.SetColor(_colorTargetMiddlePID, color.Middle);
            _quadRenderer.material.SetColor(_colorTargetBottomPID, color.Bottom);
            _quadRenderer.material.SetColor(_colorTargetPatternPID, color.Pattern);
            _lastColor = color;
        }
    }
}
