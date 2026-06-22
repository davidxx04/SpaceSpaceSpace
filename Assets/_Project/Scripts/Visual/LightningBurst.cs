using System.Collections.Generic;
using UnityEngine;

// Estallido de rayos (lightning) procedural: instancia este prefab y salen rayos quebrados y
// ramificados desde el centro, que se desvanecen, y el objeto se autodestruye. Genérico y
// reutilizable (parry, impactos del boss...).
//
// La FORMA del rayo se genera por código (no en un shader): cada rayo es una polilínea quebrada por
// "desplazamiento de punto medio" (el zigzag natural), con longitud/grosor/torcedura/bifurcaciones
// aleatorias dentro de rangos parametrizables. Cada rayo y cada bifurcación se dibuja con un
// LineRenderer de un pool, usando el shader aditivo "SpaceSpaceSpace/Lightning" (núcleo blanco +
// glow). El fundido de vida (y un parpadeo eléctrico opcional) se animan por frame.
//
// No acopla nada: solo se instancia. El material se crea en código (cacheado, compartido) y el
// fundido va por MaterialPropertyBlock -> sin instancias de material ni wiring. (En builds, añade
// "SpaceSpaceSpace/Lightning" a Always Included Shaders para que Shader.Find sobreviva al stripping.)
[DisallowMultipleComponent]
public class LightningBurst : MonoBehaviour
{
    [Header("Cantidad y dirección")]
    [SerializeField, Min(1)] private int minBolts = 4;
    [SerializeField, Min(1)] private int maxBolts = 8;
    [Tooltip("Si está marcado: un rayo principal grande hacia arriba y otro hacia abajo, más algunos secundarios.")]
    [SerializeField] private bool verticality = false;
    [Tooltip("Bamboleo (grados) de los dos rayos principales cuando 'verticality' está activo.")]
    [SerializeField] private float verticalSpread = 12f;

    [Header("Tamaño (rangos aleatorios)")]
    [Tooltip("Longitud de los rayos (unidades de mundo): aleatoria entre min y max.")]
    [SerializeField] private Vector2 lengthRange = new Vector2(1.2f, 2.5f);
    [Tooltip("Longitud de los dos rayos principales cuando 'verticality' está activo.")]
    [SerializeField] private Vector2 verticalLengthRange = new Vector2(2.5f, 4f);
    [Tooltip("Grosor base del rayo: aleatorio entre min y max.")]
    [SerializeField] private Vector2 widthRange = new Vector2(0.05f, 0.14f);

    [Header("Forma del rayo")]
    [Tooltip("Nº de tramos de cada rayo: más alto = más detalle en el zigzag.")]
    [SerializeField, Min(2)] private int segments = 8;
    [Tooltip("Torcedura/desviación del rayo (rango aleatorio). Más alto = más quebrado.")]
    [SerializeField] private Vector2 deviationRange = new Vector2(0.5f, 2f);
    [Tooltip("Nº de bifurcaciones por rayo (rango aleatorio).")]
    [SerializeField] private Vector2Int branchRange = new Vector2Int(0, 4);
    [Tooltip("Ángulo máximo (grados) con que una bifurcación se desvía de su rayo padre.")]
    [SerializeField] private float branchAngleMax = 40f;
    [Tooltip("Longitud de una bifurcación como fracción de su rayo padre (rango aleatorio).")]
    [SerializeField] private Vector2 branchLengthFrac = new Vector2(0.3f, 0.6f);
    [SerializeField, Range(0.1f, 1f)] private float branchWidthFrac = 0.6f;

    [Header("Color y vida")]
    [SerializeField] private Color color = new Color(0.7f, 0.85f, 1f, 1f);   // azul-blanco eléctrico
    [SerializeField, Min(0.001f)] private float duration = 0.25f;
    [SerializeField] private AnimationCurve alphaOverLife = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [Tooltip("Cada cuánto se regenera la forma (parpadeo eléctrico). 0 = forma estática.")]
    [SerializeField, Min(0f)] private float flickerInterval = 0.05f;
    [SerializeField] private bool useUnscaledTime = false;
    [Tooltip("Solo para afinar: repite el estallido en bucle y NO se autodestruye.")]
    [SerializeField] private bool loop = false;

    [Header("Render")]
    [SerializeField] private string sortingLayer = "Default";
    [SerializeField] private int sortingOrder = 50;

    // Mapea deviationRange (~0..4, escala "humana") a fracción de la longitud usada al desplazar.
    private const float DeviationScale = 0.12f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static Material sharedLightningMaterial;   // un material para todos los rayos (cacheado)

    private readonly List<LineRenderer> pool = new List<LineRenderer>();
    private AnimationCurve taperCurve;                 // afina el grosor hacia la punta
    private MaterialPropertyBlock mpb;
    private int activeCount;
    private float age;
    private float flickerTimer;

    private void Awake()
    {
        mpb = new MaterialPropertyBlock();
        taperCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.15f));
    }

    private void OnEnable()
    {
        age = 0f;
        flickerTimer = 0f;
        Generate();
    }

    private void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        age += dt;

        if (flickerInterval > 0f)
        {
            flickerTimer += dt;
            if (flickerTimer >= flickerInterval) { flickerTimer = 0f; Generate(); }
        }

        if (age >= duration)
        {
            if (loop) age = 0f;
            else { Destroy(gameObject); return; }
        }

        // Fundido de vida por instancia (el color base y el afilado de punta van en el vertex color).
        float t = Mathf.Clamp01(age / duration);
        float lifeAlpha = color.a * alphaOverLife.Evaluate(t);
        mpb.SetColor(ColorId, new Color(1f, 1f, 1f, lifeAlpha));
        for (int i = 0; i < activeCount; i++) pool[i].SetPropertyBlock(mpb);
    }

    // --- Generación de la forma ---

    private void Generate()
    {
        int next = 0;
        int count = Random.Range(minBolts, maxBolts + 1);

        if (verticality)
        {
            // Dos rayos principales grandes: arriba (90°) y abajo (270°), con leve bamboleo.
            float wMain = widthRange.y;
            next = EmitBolt(next, 90f + Random.Range(-verticalSpread, verticalSpread),
                            Random.Range(verticalLengthRange.x, verticalLengthRange.y), wMain);
            next = EmitBolt(next, 270f + Random.Range(-verticalSpread, verticalSpread),
                            Random.Range(verticalLengthRange.x, verticalLengthRange.y), wMain);

            int extra = Mathf.Max(0, count - 2);
            for (int i = 0; i < extra; i++)
                next = EmitBolt(next, Random.Range(0f, 360f),
                                Random.Range(lengthRange.x, lengthRange.y),
                                Random.Range(widthRange.x, widthRange.y));
        }
        else
        {
            for (int i = 0; i < count; i++)
                next = EmitBolt(next, Random.Range(0f, 360f),     // rotación aleatoria, no una rueda exacta
                                Random.Range(lengthRange.x, lengthRange.y),
                                Random.Range(widthRange.x, widthRange.y));
        }

        // Apagar las líneas sobrantes del pool.
        for (int i = next; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        activeCount = next;
    }

    // Dibuja un rayo (y sus bifurcaciones) desde el índice 'index' del pool; devuelve el siguiente libre.
    private int EmitBolt(int index, float angleDeg, float length, float width)
    {
        Vector2 dir = AngleToDir(angleDeg);
        Vector3[] pts = BuildJagged(Vector2.zero, dir, length, RandomDeviation());
        SetLine(index++, pts, width);

        int branches = Random.Range(branchRange.x, branchRange.y + 1);
        for (int b = 0; b < branches; b++)
        {
            Vector2 origin = pts[Random.Range(1, segments)];      // punto interior del rayo padre
            float bAngle = angleDeg + Random.Range(-branchAngleMax, branchAngleMax);
            float bLen = length * Random.Range(branchLengthFrac.x, branchLengthFrac.y);
            Vector3[] bpts = BuildJagged(origin, AngleToDir(bAngle), bLen, RandomDeviation());
            SetLine(index++, bpts, width * branchWidthFrac);
        }
        return index;
    }

    // Polilínea quebrada: puntos sobre la dirección, desplazados perpendicularmente al azar; el
    // desplazamiento se anula en los extremos (taper senoidal) para que arranque y termine limpios.
    private Vector3[] BuildJagged(Vector2 origin, Vector2 dir, float length, float deviationFrac)
    {
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector3[] pts = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float f = (float)i / segments;
            Vector2 basePos = origin + dir * (length * f);
            float taper = Mathf.Sin(f * Mathf.PI);                // 0 en extremos, 1 en el medio
            float off = (i == 0 || i == segments)
                ? 0f
                : Random.Range(-1f, 1f) * deviationFrac * length * taper;
            Vector2 p = basePos + perp * off;
            pts[i] = new Vector3(p.x, p.y, 0f);
        }
        return pts;
    }

    private float RandomDeviation() => Random.Range(deviationRange.x, deviationRange.y) * DeviationScale;

    private static Vector2 AngleToDir(float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
    }

    // --- Pool de LineRenderers ---

    private void SetLine(int index, Vector3[] pts, float width)
    {
        LineRenderer lr = GetLine(index);
        lr.gameObject.SetActive(true);
        lr.positionCount = pts.Length;
        lr.SetPositions(pts);
        lr.widthMultiplier = width;
        lr.widthCurve = taperCurve;
        lr.startColor = new Color(color.r, color.g, color.b, 1f);
        lr.endColor = new Color(color.r, color.g, color.b, 0f);   // se afila y desvanece hacia la punta
    }

    private LineRenderer GetLine(int index)
    {
        while (pool.Count <= index)
        {
            var go = new GameObject("Bolt" + pool.Count);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;                              // posiciones locales: el estallido va donde esté el transform
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = GetLightningMaterial();
            lr.sortingLayerName = sortingLayer;
            lr.sortingOrder = sortingOrder;
            pool.Add(lr);
        }
        return pool[index];
    }

    private static Material GetLightningMaterial()
    {
        if (sharedLightningMaterial == null)
        {
            Shader shader = Shader.Find("SpaceSpaceSpace/Lightning");
            if (shader == null)
            {
                Debug.LogError("LightningBurst: no se encontró el shader 'SpaceSpaceSpace/Lightning' " +
                               "(¿error de compilación en Lightning.shader?). Revisa la consola.");
                return null;
            }
            sharedLightningMaterial = new Material(shader);
        }
        return sharedLightningMaterial;
    }
}
