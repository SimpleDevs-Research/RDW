using UnityEngine;

[ExecuteAlways, RequireComponent(typeof(Renderer))]
public class TextureScaleFixer : MonoBehaviour
{
    private Renderer _renderer;

    void Start() {
        _renderer = GetComponent<Renderer>();
    }

    void Update() {
        Vector3 scale = transform.lossyScale;
        _renderer.sharedMaterial.mainTextureScale = new Vector2(scale.x, scale.y);
    }
}
