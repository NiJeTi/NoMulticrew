using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NoMulticrew.Client.Screens;

internal sealed class ScreenQuad : IDisposable
{
    private readonly Mesh _mesh;
    private readonly MeshRenderer _renderer;

    public ScreenQuad(
        string name,
        Transform parent,
        int layer,
        Material material,
        Vector3[] corners,
        Vector2[] uvs,
        Vector3 normal
    )
    {
        _mesh = new Mesh
        {
            name = name,
            vertices = corners,
            uv = uvs,
            normals = [normal, normal, normal, normal],
            triangles = [0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2],
        };
        _mesh.RecalculateBounds();

        var quad = new GameObject(name) { layer = layer };
        quad.transform.SetParent(parent, false);
        quad.AddComponent<MeshFilter>().sharedMesh = _mesh;

        _renderer = quad.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial = material;
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
    }

    public void Show(bool visible)
    {
        if (_renderer != null)
        {
            _renderer.enabled = visible;
        }
    }

    public void Dispose()
    {
        if (_renderer != null)
        {
            Object.Destroy(_renderer.gameObject);
        }

        Object.Destroy(_mesh);
    }
}
