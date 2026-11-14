using UnityEngine;

namespace LKS.World
{
    [ExecuteAlways]
    [RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
    public sealed class StationLabel : MonoBehaviour
    {
        private static readonly int MainTexture = Shader.PropertyToID("_MainTex");
        private TextMesh label;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock properties;

        private void OnEnable()
        {
            label = GetComponent<TextMesh>();
            meshRenderer = GetComponent<MeshRenderer>();
            properties ??= new MaterialPropertyBlock();
            Font.textureRebuilt += BindAtlas;
            Refresh();
        }

        private void OnDisable() => Font.textureRebuilt -= BindAtlas;

        public void SetText(string text)
        {
            GetComponent<TextMesh>().text = text;
            if (isActiveAndEnabled) Refresh();
        }

        private void Refresh()
        {
            if (label.font == null) return;
            label.font.RequestCharactersInTexture(label.text, label.fontSize, label.fontStyle);
            BindAtlas(label.font);
        }

        private void BindAtlas(Font font)
        {
            if (font != label.font || font.material.mainTexture == null) return;
            meshRenderer.GetPropertyBlock(properties);
            properties.SetTexture(MainTexture, font.material.mainTexture);
            meshRenderer.SetPropertyBlock(properties);
        }
    }
}
