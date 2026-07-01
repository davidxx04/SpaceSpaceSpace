using UnityEngine;

// Fondo espacial full-screen: monta un quad con el shader SpaceSpaceSpace/SpaceNebula, lo mantiene
// cubriendo la vista de una cámara ORTOGRÁFICA fija (arena) y le pasa el aspecto para que el ruido no
// salga estirado. La animación (nebulosa + estrellas parallax) vive en el shader (_Time), aquí la CPU
// solo reescala/reposiciona. Sigue el estilo de BossArea (quad procedural + material cacheado).
//
// [ExecuteAlways] para verlo/ajustarlo en edición. Se cuelga bajo la cámara (lo hace BackgroundBuilder)
// para heredar el CameraShake sin bordes, pero también funciona suelto (reposiciona por LateUpdate).
[ExecuteAlways]
[DisallowMultipleComponent]
public class SpaceBackground : MonoBehaviour
{
    [Tooltip("Material con el shader SpaceNebula. Si es null, se crea uno desde el shader (no tuneable como asset).")]
    [SerializeField] private Material material;

    [Tooltip("Orden de dibujado: muy negativo para quedar detrás de TODO el gameplay.")]
    [SerializeField] private int sortingOrder = -100;

    [Tooltip("Distancia delante de la cámara (dentro del frustum, detrás del gameplay).")]
    [SerializeField] private float distanceFromCamera = 12f;

    [Tooltip("Margen extra de cobertura (1 = exacto a la vista).")]
    [SerializeField] private float margin = 1.05f;

    [Tooltip("Cámara a cubrir. Si null: la del padre o Camera.main.")]
    [SerializeField] private Camera targetCamera;

    private SpriteRenderer quad;
    private MaterialPropertyBlock mpb;
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");
    private static Material fallbackMaterial;

    private void OnEnable()
    {
        EnsureQuad();
        Apply();
    }

    private void LateUpdate()
    {
        // LateUpdate: después de mover cámara/gameplay (cubre bien aunque haya CameraShake).
        if (quad == null) EnsureQuad();
        Apply();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // No creamos GameObjects en OnValidate (no está permitido); solo re-aplicamos si ya existe.
        if (quad != null)
        {
            quad.sortingOrder = sortingOrder;
            quad.sharedMaterial = ResolveMaterial();
            Apply();
        }
    }
#endif

    private Camera ResolveCamera()
    {
        if (targetCamera != null) return targetCamera;
        var parentCam = GetComponentInParent<Camera>();
        return parentCam != null ? parentCam : Camera.main;
    }

    private void EnsureQuad()
    {
        if (quad == null)
        {
            var existing = transform.Find("NebulaQuad");
            quad = existing != null ? existing.GetComponent<SpriteRenderer>() : null;
        }
        if (quad == null)
        {
            var go = new GameObject("NebulaQuad");
            go.transform.SetParent(transform, false);
            quad = go.AddComponent<SpriteRenderer>();
        }

        quad.sprite = PrimitiveQuad.Unit;
        quad.sortingOrder = sortingOrder;
        quad.sharedMaterial = ResolveMaterial();
    }

    // Material tuneable (asset) si se cableó; si no, uno auto desde el shader (compartido, cacheado).
    private Material ResolveMaterial()
    {
        if (material != null) return material;
        if (fallbackMaterial == null)
        {
            Shader s = Shader.Find("SpaceSpaceSpace/SpaceNebula");
            if (s != null) fallbackMaterial = new Material(s) { name = "SpaceNebula (auto)" };
        }
        return fallbackMaterial;
    }

    private void Apply()
    {
        if (quad == null) return;
        Camera cam = ResolveCamera();
        if (cam == null || !cam.orthographic) return;

        float h = cam.orthographicSize * 2f;
        float w = h * cam.aspect;

        Transform t = quad.transform;
        t.position = cam.transform.position + cam.transform.forward * distanceFromCamera;
        t.rotation = cam.transform.rotation;
        t.localScale = new Vector3(w * margin, h * margin, 1f);

        // _Aspect (= ancho/alto de la vista) por MaterialPropertyBlock: no muta el .mat compartido.
        if (mpb == null) mpb = new MaterialPropertyBlock();
        quad.GetPropertyBlock(mpb);
        mpb.SetFloat(AspectId, cam.aspect);
        quad.SetPropertyBlock(mpb);
    }
}
