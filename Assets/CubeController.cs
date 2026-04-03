using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;


[RequireComponent(typeof(Rigidbody))]
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
    public float shrinkRate = 0.0004f;
    public float shrinkPerSpeed = 0.0000005f;

    [Header("Física")]
    [Tooltip("Masa inicial del cubo")]
    public float initialMass = 2f;

    [Tooltip("Masa mínima cuando el cubo es muy pequeño")]
    public float minMass = 0.1f;

    [Tooltip("Multiplicador de la gravedad superficial (cuánto empuja la inclinación)")]
    public float gravityScale = 15f;

    [Tooltip("Velocidad máxima permitida en espacio mundo")]
    public float maxSpeed = 10f;

    [Tooltip("Fricción del tablero (reduce velocidad cuando hay poca inclinación)")]
    [Range(0f, 1f)]
    public float surfaceFriction = 0.05f;

    [HideInInspector] public bool reachedGoal = false;

    // ─────────────────────────────────────────
    //  ALINEACIÓN AL TABLERO
    // ─────────────────────────────────────────
    [Header("Alineación al tablero")]
    [Tooltip("Qué tan rápido el cubo se alinea visualmente con la inclinación del tablero")]
    public float alignmentSpeed = 8f;
  

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

        currentScale = initialScale;
        totalShrinkRange = 20+ initialScale - minScale;

        transform.localScale = Vector3.one * currentScale;

        SetupRigidbody();

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
            PhysicMaterial mat = new PhysicMaterial();
            mat.dynamicFriction = 0f;
            mat.staticFriction = 0f;
            mat.bounciness = 0.1f;
            mat.frictionCombine = PhysicMaterialCombine.Minimum;
            mat.bounceCombine = PhysicMaterialCombine.Minimum;
            col.material = mat;
        }
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

    void OnCollisionEnter(Collision collision)
    {
        if (isDead) return;
        
        if (collision.gameObject.CompareTag("deadzone"))
        {
            TriggerFail();
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (sceneController != null && collision.gameObject == sceneController.gameObject)
        {
            isGrounded = false;
        }
    }

    void TriggerFail()
    {
        isDead = true;

        rb.velocity = Vector3.zero;
        rb.isKinematic = true;

        Debug.Log("[CubeController] Player murió por colisión.");


        // Reiniciar nivel después de un pequeño delay (opcional)
        RestartLevel();
    }

    void RestartLevel()
    {
        Time.timeScale = 1f;
        Application.LoadLevel(Application.loadedLevelName);
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
        if (isGrounded && rb.velocity.magnitude > 0.01f)
        {
            Vector3 frictionForce = -rb.velocity * surfaceFriction;
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

    // Modifica Shrink() así:
    void Shrink()
    {
        if (reachedGoal) return; 

        float speed = rb.velocity.magnitude;
        currentScale -= shrinkPerSpeed * speed / (3);

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
    }

    // ─────────────────────────────────────────────────────────────────────
    //  LÍMITES — El cubo no puede salir del tablero
    // ─────────────────────────────────────────────────────────────────────
    void ClampPositionToBoard()
    {
        if (transform.parent == null) return;

        Vector3 localPos = transform.localPosition;
        Vector3 localVel = transform.parent.InverseTransformDirection(rb.velocity);
        float halfCube = currentScale * 0.5f;
        float softZone = 0.3f; // zona de frenado antes del muro
        bool hitWall = false;

        // Eje X
        float limitX = boardHalfX - halfCube;
        if (localPos.x > limitX)
        {
            localPos.x = limitX;
            if (localVel.x > 0) localVel.x = -localVel.x * 0.4f;
            hitWall = true;
        }
        else if (localPos.x < -limitX)
        {
            localPos.x = -limitX;
            if (localVel.x < 0) localVel.x = -localVel.x * 0.4f;
            hitWall = true;
        }
        else if (localPos.x > limitX - softZone && localVel.x > 0)
        {
            // Frenar suavemente al acercarse al borde
            float t = (localPos.x - (limitX - softZone)) / softZone;
            localVel.x -= localVel.x * t * 0.15f;
        }
        else if (localPos.x < -(limitX - softZone) && localVel.x < 0)
        {
            float t = (-localPos.x - (limitX - softZone)) / softZone;
            localVel.x -= localVel.x * t * 0.15f;
        }

        // Eje Z
        float limitZ = boardHalfZ - halfCube;
        if (localPos.z > limitZ)
        {
            localPos.z = limitZ;
            if (localVel.z > 0) localVel.z = -localVel.z * 0.4f;
            hitWall = true;
        }
        else if (localPos.z < -limitZ)
        {
            localPos.z = -limitZ;
            if (localVel.z < 0) localVel.z = -localVel.z * 0.4f;
            hitWall = true;
        }
        else if (localPos.z > limitZ - softZone && localVel.z > 0)
        {
            float t = (localPos.z - (limitZ - softZone)) / softZone;
            localVel.z -= localVel.z * t * 0.15f;
        }
        else if (localPos.z < -(limitZ - softZone) && localVel.z < 0)
        {
            float t = (-localPos.z - (limitZ - softZone)) / softZone;
            localVel.z -= localVel.z * t * 0.15f;
        }

        transform.localPosition = localPos;
        rb.velocity = transform.parent.TransformDirection(localVel);
        
    }

    void BounceOnAxis(ref Vector3 localPos, char axis, bool positive)
    {
        Transform board = transform.parent;
        Vector3 localVel = board.InverseTransformDirection(rb.velocity);

        if (axis == 'x')
            localVel.x = positive ? -Mathf.Abs(localVel.x) * 0.4f
                                   : Mathf.Abs(localVel.x) * 0.4f;
        else
            localVel.z = positive ? -Mathf.Abs(localVel.z) * 0.4f
                                   : Mathf.Abs(localVel.z) * 0.4f;

        rb.velocity = board.TransformDirection(localVel);
    }
    // ─────────────────────────────────────────────────────────────────────
    //  MUERTE
    // ─────────────────────────────────────────────────────────────────────
    void OnCubeDied()
    {
        rb.velocity = Vector3.zero;
        rb.isKinematic = true;
        Debug.Log("[CubeController] Cubo llegó a tamaño mínimo.");
    }

    void DeactivateCube()
    {
        gameObject.SetActive(false);
    }

    float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}