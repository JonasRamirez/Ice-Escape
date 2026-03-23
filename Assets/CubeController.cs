using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla el cubo que:
///   - Se orienta automáticamente al ángulo del tablero (se adapta a la inclinación).
///   - Recibe una gravedad simulada proyectada sobre la superficie del tablero,
///     por lo que rueda en la dirección de la inclinación.
///   - Se encoge progresivamente — cuanto más pequeño, menos masa, más velocidad.
///   - Deja un rastro (TrailRenderer) que se ajusta a su tamaño.
///
/// Compatible con Unity 2017.4.40f1 LTS
///
/// SETUP REQUERIDO:
///   1. El cubo debe ser hijo del GameObject del tablero.
///   2. El cubo necesita: Rigidbody + BoxCollider + TrailRenderer.
///   3. Rigidbody → Use Gravity: FALSE  (la gravedad la calculamos nosotros).
///   4. Rigidbody → Freeze Rotation: X Y Z  (el cubo se orienta con transform, no con física).
///   5. Rigidbody → Freeze Position: Y  NO activar (necesitamos el eje Y libre para el contacto).
///
/// CÓMO FUNCIONA LA FÍSICA:
///   La gravedad real de Unity siempre apunta hacia abajo en mundo.
///   Cuando el tablero se inclina, queremos que el cubo se deslice "cuesta abajo".
///   Para eso, tomamos la normal del tablero (BoardUp) y proyectamos la gravedad
///   sobre el plano del tablero: esa componente tangencial es la fuerza que empuja al cubo.
///   La componente normal la cancelamos, dejando solo la deslizante.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(TrailRenderer))]
public class CubeController : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  REFERENCIAS
    // ─────────────────────────────────────────
    [Header("Referencia al Tablero")]
    [Tooltip("Arrastra aquí el GameObject del tablero (el que tiene GyroscopeSceneController)")]
    public GyroscopeSceneController boardController;

    // ─────────────────────────────────────────
    //  ENCOGIMIENTO
    // ─────────────────────────────────────────
    [Header("Encogimiento")]
    [Tooltip("Escala inicial del cubo")]
    public float initialScale = 1f;

    [Tooltip("Escala mínima antes de desaparecer")]
    public float minScale = 0.05f;

    [Tooltip("Velocidad de encogimiento (unidades/segundo)")]
    public float shrinkRate = 0.04f;

    // ─────────────────────────────────────────
    //  FÍSICA
    // ─────────────────────────────────────────
    [Header("Física")]
    [Tooltip("Masa inicial del cubo")]
    public float initialMass = 2f;

    [Tooltip("Masa mínima cuando el cubo es muy pequeño")]
    public float minMass = 0.1f;

    [Tooltip("Multiplicador de la gravedad superficial (cuánto empuja la inclinación)")]
    public float gravityScale = 15f;

    [Tooltip("Velocidad máxima permitida en espacio mundo")]
    public float maxSpeed = 12f;

    [Tooltip("Fricción del tablero (reduce velocidad cuando hay poca inclinación)")]
    [Range(0f, 1f)]
    public float surfaceFriction = 0.05f;

    // ─────────────────────────────────────────
    //  ALINEACIÓN AL TABLERO
    // ─────────────────────────────────────────
    [Header("Alineación al tablero")]
    [Tooltip("Qué tan rápido el cubo se alinea visualmente con la inclinación del tablero")]
    public float alignmentSpeed = 8f;

    // ─────────────────────────────────────────
    //  RASTRO
    // ─────────────────────────────────────────
    [Header("Rastro")]
    public float trailTime = 1.2f;
    public float trailStartWidth = 0.3f;
    public Color trailStartColor = new Color(0.2f, 0.8f, 1f, 0.9f);
    public Color trailEndColor = new Color(0.2f, 0.8f, 1f, 0f);

    // ─────────────────────────────────────────
    //  LÍMITES DEL TABLERO
    // ─────────────────────────────────────────
    [Header("Límites del tablero (espacio local del tablero)")]
    public float boardHalfX = 4.5f;
    public float boardHalfZ = 4.5f;

    // ─────────────────────────────────────────
    //  PRIVADOS
    // ─────────────────────────────────────────
    private Rigidbody rb;
    private TrailRenderer trail;
    private float currentScale;
    private float totalShrinkRange;
    private bool isDead = false;
    private bool isGrounded = false;
    private Vector3 lastDebugForce = Vector3.zero;
    private GyroscopeSceneController sceneController; // Variable local para el controlador

    // ─────────────────────────────────────────────────────────────────────
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        trail = GetComponent<TrailRenderer>();

        currentScale = initialScale;
        totalShrinkRange = initialScale - minScale;

        transform.localScale = Vector3.one * currentScale;

        SetupRigidbody();
        SetupTrail();

        // Buscar el controlador automáticamente
        if (boardController == null && transform.parent != null)
        {
            // Intentar obtener el componente del padre
            sceneController = transform.parent.GetComponent<GyroscopeSceneController>();
            if (sceneController == null)
            {
                // Si no está en el padre, buscar en toda la escena
                sceneController = FindObjectOfType<GyroscopeSceneController>();
                if (sceneController != null)
                {
                    Debug.Log("[CubeController] Controlador encontrado en la escena: " + sceneController.gameObject.name);
                }
            }
            else
            {
                Debug.Log("[CubeController] Controlador encontrado en el padre: " + sceneController.gameObject.name);
            }
        }
        else if (boardController != null)
        {
            sceneController = boardController;
            Debug.Log("[CubeController] Usando controlador asignado manualmente");
        }
        else
        {
            // Último intento: buscar por nombre
            GameObject laberynth = GameObject.Find("Laberynth");
            if (laberynth != null)
            {
                sceneController = laberynth.GetComponent<GyroscopeSceneController>();
                if (sceneController != null)
                {
                    Debug.Log("[CubeController] Controlador encontrado por nombre: Laberynth");
                }
            }
        }

        if (sceneController == null)
        {
            Debug.LogError("[CubeController] ¡No se pudo encontrar GyroscopeSceneController! Asegúrate de que el cubo es hijo del tablero o asigna manualmente boardController.");
        }
    }

    void SetupRigidbody()
    {
        rb.mass = initialMass;
        rb.drag = 0f;
        rb.angularDrag = 0f;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            col.material = new PhysicMaterial();
            col.material.dynamicFriction = 0.2f;
            col.material.staticFriction = 0.2f;
            col.material.bounciness = 0.3f;
        }
    }

    void SetupTrail()
    {
        trail.time = trailTime;
        trail.startWidth = trailStartWidth;
        trail.endWidth = 0f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        ApplyTrailGradient(trailStartColor, trailEndColor);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  DETECCIÓN DE COLISIONES
    // ─────────────────────────────────────────────────────────────────────
    void OnCollisionStay(Collision collision)
    {
        // Verificar si estamos colisionando con el tablero
        if (sceneController != null && collision.gameObject == sceneController.gameObject)
        {
            isGrounded = true;
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (sceneController != null && collision.gameObject == sceneController.gameObject)
        {
            isGrounded = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  FÍSICA — FixedUpdate para movimiento por inclinación
    // ─────────────────────────────────────────────────────────────────────
    void FixedUpdate()
    {
        if (isDead || sceneController == null) return;

        ApplySurfaceGravity();
        ApplyFriction();
        ClampSpeed();
        ClampPositionToBoard();
    }

    /// <summary>
    /// Proyecta la gravedad global sobre el plano del tablero.
    /// Resultado: el cubo se desliza en la dirección de la pendiente.
    /// </summary>
    void ApplySurfaceGravity()
    {
        if (sceneController == null) return;

        Vector3 boardNormal = sceneController.BoardUp;   // Normal del tablero en mundo
        Vector3 gravity = Vector3.down * gravityScale;

        // Componente de la gravedad paralela a la superficie (la que produce deslizamiento)
        Vector3 gravityOnSurface = gravity - Vector3.Dot(gravity, boardNormal) * boardNormal;

        lastDebugForce = gravityOnSurface;

        // Solo aplicar gravedad si está en contacto con el tablero
        if (isGrounded)
        {
            rb.AddForce(gravityOnSurface, ForceMode.Acceleration);
        }
        else
        {
            // Si está en el aire, aplicar gravedad normal hacia abajo
            rb.AddForce(Vector3.down * gravityScale, ForceMode.Acceleration);
        }
    }

    /// <summary>
    /// Aplica fricción manual para evitar que el cubo siga acelerando infinitamente
    /// cuando el tablero está casi horizontal.
    /// </summary>
    void ApplyFriction()
    {
        if (isGrounded && rb.velocity.magnitude > 0.05f)
        {
            float frictionMultiplier = Mathf.Lerp(surfaceFriction * 2f, surfaceFriction, rb.velocity.magnitude / 2f);
            Vector3 frictionForce = -rb.velocity.normalized * frictionMultiplier * gravityScale * 0.5f;
            rb.AddForce(frictionForce, ForceMode.Acceleration);
        }
    }

    void ClampSpeed()
    {
        if (rb.velocity.magnitude > maxSpeed)
            rb.velocity = rb.velocity.normalized * maxSpeed;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  UPDATE — Encogimiento + alineación + rastro
    // ─────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (isDead) return;

        AlignToBoardSurface();
        Shrink();
        UpdatePhysicsBasedOnSize();
    }

    /// <summary>
    /// Rota el cubo para que su cara inferior quede paralela al tablero.
    /// Esto da el efecto de que "se adapta" a la inclinación visualmente.
    /// </summary>
    void AlignToBoardSurface()
    {
        if (sceneController == null) return;

        // La rotación objetivo es la misma que la del tablero padre
        Quaternion targetRotation = sceneController.transform.rotation;

        transform.rotation = Quaternion.Slerp(
            transform.rotation, targetRotation, Time.deltaTime * alignmentSpeed);
    }

    void Shrink()
    {
        currentScale -= shrinkRate * Time.deltaTime;

        if (currentScale <= minScale)
        {
            currentScale = minScale;
            isDead = true;
            OnCubeDied();
            return;
        }

        transform.localScale = Vector3.one * currentScale;
    }

    void UpdatePhysicsBasedOnSize()
    {
        float lifePercent = (currentScale - minScale) / totalShrinkRange;

        rb.mass = Mathf.Lerp(minMass, initialMass, lifePercent);
        maxSpeed = Mathf.Lerp(18f, 6f, lifePercent);
        trail.startWidth = trailStartWidth * (currentScale / initialScale);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  LÍMITES — El cubo no puede salir del tablero
    // ─────────────────────────────────────────────────────────────────────
    void ClampPositionToBoard()
    {
        if (transform.parent == null) return;

        Vector3 localPos = transform.localPosition;
        float halfCube = currentScale * 0.5f;
        bool hitWall = false;

        if (localPos.x > boardHalfX - halfCube)
        {
            localPos.x = boardHalfX - halfCube;
            BounceOnAxis(ref localPos, 'x', true);
            hitWall = true;
        }
        else if (localPos.x < -(boardHalfX - halfCube))
        {
            localPos.x = -(boardHalfX - halfCube);
            BounceOnAxis(ref localPos, 'x', false);
            hitWall = true;
        }

        if (localPos.z > boardHalfZ - halfCube)
        {
            localPos.z = boardHalfZ - halfCube;
            BounceOnAxis(ref localPos, 'z', true);
            hitWall = true;
        }
        else if (localPos.z < -(boardHalfZ - halfCube))
        {
            localPos.z = -(boardHalfZ - halfCube);
            BounceOnAxis(ref localPos, 'z', false);
            hitWall = true;
        }

        transform.localPosition = localPos;

        if (hitWall) FlashTrail();
    }

    void BounceOnAxis(ref Vector3 localPos, char axis, bool positive)
    {
        Transform board = transform.parent;
        Vector3 localVel = board.InverseTransformDirection(rb.velocity);

        if (axis == 'x')
            localVel.x = positive ? -Mathf.Abs(localVel.x) * 0.55f
                                   : Mathf.Abs(localVel.x) * 0.55f;
        else
            localVel.z = positive ? -Mathf.Abs(localVel.z) * 0.55f
                                   : Mathf.Abs(localVel.z) * 0.55f;

        rb.velocity = board.TransformDirection(localVel);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  RASTRO — efectos de color
    // ─────────────────────────────────────────────────────────────────────
    void FlashTrail()
    {
        ApplyTrailGradient(Color.yellow, new Color(1f, 0.5f, 0f, 0f));
        Invoke("RestoreTrailColor", 0.15f);
    }

    void RestoreTrailColor()
    {
        ApplyTrailGradient(trailStartColor, trailEndColor);
    }

    void ApplyTrailGradient(Color start, Color end)
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(start, 0f),
                new GradientColorKey(end,   1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(start.a, 0f),
                new GradientAlphaKey(0f,      1f)
            }
        );
        trail.colorGradient = g;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  MUERTE
    // ─────────────────────────────────────────────────────────────────────
    void OnCubeDied()
    {
        rb.velocity = Vector3.zero;
        rb.isKinematic = true;
        Invoke("DeactivateCube", trailTime + 0.5f);
        Debug.Log("[CubeController] Cubo llegó a tamaño mínimo.");
    }

    void DeactivateCube()
    {
        gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  DEBUG EN PANTALLA
    // ─────────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        if (isDead) return;
        float life = (currentScale - minScale) / totalShrinkRange * 100f;

        GUI.Box(new Rect(10, 80, 260, 120), "Información del Cubo");
        GUI.Label(new Rect(20, 100, 240, 20), string.Format("Tamaño : {0:F2}", currentScale));
        GUI.Label(new Rect(20, 120, 240, 20), string.Format("Masa   : {0:F2} kg", rb.mass));
        GUI.Label(new Rect(20, 140, 240, 20), string.Format("Vel    : {0:F2} m/s", rb.velocity.magnitude));
        GUI.Label(new Rect(20, 160, 240, 20), string.Format("Vida   : {0:F0}%", life));
        GUI.Label(new Rect(20, 180, 240, 20), string.Format("Contacto: {0}", isGrounded ? "Sí" : "No"));

        if (sceneController != null)
        {
            Vector3 tilt = sceneController.transform.rotation.eulerAngles;
            GUI.Label(new Rect(10, 210, 260, 20),
                string.Format("Inclinación X:{0:F1}° Z:{1:F1}°",
                    NormalizeAngle(tilt.x), NormalizeAngle(tilt.z)));
        }
    }

    float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}