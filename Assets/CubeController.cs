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
    public float shrinkRate = 0.00004f;
    public float shrinkPerSpeed = 0.00000005f;

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
    public float alignmentSpeed = 16f;


    // ─────────────────────────────────────────
    //  LÍMITES DEL TABLERO
    // ─────────────────────────────────────────
    [Header("Límites del tablero (espacio local del tablero)")]
    public float boardHalfX = 4.5f;
    public float boardHalfZ = 4.5f;

    [Header("Feedback de impacto")]
    [Tooltip("AudioSource con el sonido de choque")]
    public AudioSource impactSound;

    [Header("Sonidos Variados")]
    [Tooltip("Array de sonidos de impacto (seleccionará uno aleatorio)")]
    public AudioClip[] impactSounds;

    [Tooltip("Si está activado, varía ligeramente el pitch del sonido")]
    public bool randomizePitch = true;

    [Tooltip("Rango de variación del pitch (mínimo y máximo)")]
    public Vector2 pitchRange = new Vector2(0.9f, 1.1f);

    [Tooltip("Duración de la vibración en milisegundos")]
    public long vibrationDuration = 30;

    [Tooltip("Duración de la vibración al romperse (más fuerte)")]
    public long breakVibrationDuration = 80;

    [Tooltip("Pequeño delay antes de reiniciar para que el feedback de rotura se alcance a percibir")]
    public float breakRestartDelay = 0.9f;

    [Tooltip("Tiempo mínimo entre sonidos de impacto para evitar solaparlos demasiado")]
    public float impactFeedbackCooldown = 0.1f;

    [Tooltip("Lanza una vibración de prueba al iniciar en dispositivo para verificar que el hardware responde")]
    public bool testVibrationOnStart = false;

    [Header("Impacto por velocidad")]
    [Tooltip("Velocidad mínima de impacto para reproducir feedback (m/s)")]
    public float impactBreakThreshold = 2.4f;

    [Tooltip("Velocidad mínima para romperse al chocar contra una pared (m/s)")]
    public float impactDestroy = 6.5f;

    [Header("Rotura del hielo")]
    [Tooltip("Número de fragmentos por eje. Total = X * Y * Z")]
    public Vector3Int breakPieces = new Vector3Int(1, 1, 1);

    [Tooltip("Separación visual entre fragmentos al generarlos")]
    public float breakGap = 0.015f;

    [Tooltip("Fuerza con la que salen disparados los pedazos")]
    public float breakExplosionForce = 1f;

    [Tooltip("Impulso extra hacia arriba para que la rotura se lea mejor")]
    public float breakUpwardForce = 0.0005f;

    [Tooltip("Torque aleatorio aplicado a cada fragmento")]
    public float breakTorque = 2f;

    [Tooltip("Tiempo de vida de los fragmentos antes de destruirse")]
    public float breakFragmentLifetime = 2f;

    [Tooltip("Si la normal del contacto es muy vertical, se considera piso y no pared")]
    [Range(0f, 1f)]
    public float wallNormalLimit = 0.45f;


    // ─────────────────────────────────────────
    //  PRIVADOS
    // ─────────────────────────────────────────
    private Rigidbody rb;
    private float currentScale;
    private float totalShrinkRange;
    private bool isDead = false;
    private bool isGrounded = false;
    public bool IsGrounded { get { return isGrounded; } }
    private Vector3 lastDebugForce = Vector3.zero;
    private GyroscopeSceneController sceneController; // Variable local para el controlador
    private Transform boardTransform;
    private float lastImpactFeedbackTime = float.NegativeInfinity;
    private float originalPitch; // Para guardar el pitch original del AudioSource
    private Coroutine restartCoroutine;
    private Renderer cachedRenderer;
    private Collider cachedCollider;

    // ─────────────────────────────────────────────────────────────────────
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        cachedRenderer = GetComponent<Renderer>();
        cachedCollider = GetComponent<Collider>();

        currentScale = initialScale;
        totalShrinkRange = 20 + initialScale - minScale;

        transform.localScale = Vector3.one * currentScale;

        SetupRigidbody();
        ConfigureImpactSound();

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

        InitializeVibration();

        if (testVibrationOnStart)
        {
            StartCoroutine(TestVibrationOnStart());
        }

        if (sceneController == null)
        {
            Debug.LogError("[CubeController] ¡No se pudo encontrar GyroscopeSceneController! Asegúrate de que el cubo es hijo del tablero o asigna manualmente boardController.");
            return;
        }

        boardTransform = sceneController.transform;

        // Un Rigidbody no debe heredar la rotación del tablero o deja de responder de forma natural.
        if (transform.parent == boardTransform)
        {
            transform.SetParent(null, true);
        }
    }

    void SetupRigidbody()
    {
        rb.mass = initialMass;
        rb.drag = 0f;
        rb.angularDrag = 0f;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
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

    void ConfigureImpactSound()
    {
        if (impactSound == null)
        {
            impactSound = GetComponent<AudioSource>();
        }

        if (impactSound == null)
        {
            impactSound = GetComponentInChildren<AudioSource>(true);
        }

        if (impactSound == null)
        {
            Debug.LogWarning("[CubeController] No se encontró AudioSource para impactos en " + gameObject.name + ".");
            return;
        }

        impactSound.playOnAwake = false;
        impactSound.loop = false;

        // Guardar el pitch original
        if (impactSound != null)
        {
            originalPitch = impactSound.pitch;
        }
    }


    // ─────────────────────────────────────────────────────────────────────
    //  DETECCIÓN DE COLISIONES
    // ─────────────────────────────────────────────────────────────────────
    void OnCollisionStay(Collision collision)
    {
        if (boardTransform != null && collision.transform.IsChildOf(boardTransform))
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
            return;
        }

        float impactForce = collision.relativeVelocity.magnitude;
        Debug.Log("Impact force: " + impactForce);
        bool isWallImpact = IsWallCollision(collision);

        float feedbackThreshold = Mathf.Max(0.01f, Mathf.Min(impactBreakThreshold, impactDestroy));
        float destroyThreshold = Mathf.Max(impactBreakThreshold, impactDestroy);

        // Impacto No tan fuerte
        if (impactForce >= feedbackThreshold)
        {
            TriggerImpactFeedback();
        }

        // Impacto FUERTE 
        if (isWallImpact && impactForce >= destroyThreshold)
        {
            TriggerBreak(collision);
            return;
        }

    }

    void OnCollisionExit(Collision collision)
    {
        if (boardTransform != null && collision.transform.IsChildOf(boardTransform))
        {
            isGrounded = false;
        }
    }

    bool IsWallCollision(Collision collision)
    {
        if (collision == null || collision.contacts == null || collision.contacts.Length == 0)
        {
            return false;
        }

        Vector3 upReference = boardTransform != null ? boardTransform.up : Vector3.up;

        for (int i = 0; i < collision.contacts.Length; i++)
        {
            ContactPoint contact = collision.contacts[i];
            float alignment = Mathf.Abs(Vector3.Dot(contact.normal.normalized, upReference));
            if (alignment <= wallNormalLimit)
            {
                return true;
            }
        }

        return false;
    }

    void TriggerBreak(Collision collision)
    {
        isDead = true;
        Vector3 preBreakVelocity = rb.velocity;

        rb.velocity = Vector3.zero;
        rb.isKinematic = true;
        rb.detectCollisions = false;

        Debug.Log("[CubeController] Cubo se rompió por impacto fuerte.");

        // Vibración más fuerte al romperse
        Vibrate(breakVibrationDuration, true);

        SpawnBreakFragments(collision, preBreakVelocity);
        HideOriginalCube();

        if (restartCoroutine != null)
        {
            StopCoroutine(restartCoroutine);
        }

        restartCoroutine = StartCoroutine(RestartLevelAfterDelay(breakRestartDelay));
    }

    void HideOriginalCube()
    {
        if (cachedRenderer != null)
        {
            cachedRenderer.enabled = false;
        }

        if (cachedCollider != null)
        {
            cachedCollider.enabled = false;
        }
    }

    void SpawnBreakFragments(Collision collision, Vector3 preBreakVelocity)
    {
        // Forzar al menos 2x2x2 fragmentos para que se vea bien
        int piecesX = Mathf.Max(2, breakPieces.x);
        int piecesY = Mathf.Max(2, breakPieces.y);
        int piecesZ = Mathf.Max(2, breakPieces.z);
        int totalPieces = piecesX * piecesY * piecesZ;

        Vector3 pieceSize = new Vector3(
            transform.lossyScale.x / piecesX,
            transform.lossyScale.y / piecesY,
            transform.lossyScale.z / piecesZ);
        
        float smallerPieces = 0.9f;
        pieceSize *= smallerPieces;

        float sizeMultiplier = currentScale * currentScale;

        for (int i = 0; i < totalPieces; i++)
        {
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.transform.SetPositionAndRotation(transform.position, transform.rotation);
            shard.transform.localScale = pieceSize - Vector3.one * breakGap;

            Renderer shardRenderer = shard.GetComponent<Renderer>();
            if (shardRenderer != null && cachedRenderer != null)
                shardRenderer.sharedMaterial = cachedRenderer.sharedMaterial;

            Rigidbody shardRb = shard.AddComponent<Rigidbody>();
            shardRb.mass = rb.mass / totalPieces;
            shardRb.interpolation = RigidbodyInterpolation.Interpolate;
            
            Vector3 randomDir = Random.onUnitSphere;
            randomDir.y = Mathf.Abs(randomDir.y); 
            
            float forceMultiplier = 0.005f;
            shardRb.AddForce(randomDir * breakExplosionForce * sizeMultiplier * forceMultiplier, ForceMode.Force);
            shardRb.AddTorque(Random.insideUnitSphere * breakTorque * sizeMultiplier * 0.1f, ForceMode.Force);

            Destroy(shard, breakFragmentLifetime);
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


    void TriggerImpactFeedback()
    {
        if (Time.time - lastImpactFeedbackTime < impactFeedbackCooldown) return;

        lastImpactFeedbackTime = Time.time;
        PlayRandomImpactSound();
    }

    void PlayRandomImpactSound()
    {
        if (impactSound == null)
        {
            ConfigureImpactSound();
            if (impactSound == null) return;
        }

        // Seleccionar un sonido aleatorio del array
        AudioClip clipToPlay = null;

        if (impactSounds != null && impactSounds.Length > 0)
        {
            // Filtrar clips nulos
            List<AudioClip> validClips = new List<AudioClip>();
            foreach (var clip in impactSounds)
            {
                if (clip != null) validClips.Add(clip);
            }

            if (validClips.Count > 0)
            {
                int randomIndex = Random.Range(0, validClips.Count);
                clipToPlay = validClips[randomIndex];
                Debug.Log("[CubeController] Reproduciendo sonido de impacto " + (randomIndex + 1) + " de " + validClips.Count);
            }
        }

        // Si no hay sonidos en el array o todos son nulos, usar el clip por defecto del AudioSource
        if (clipToPlay == null && impactSound.clip != null)
        {
            clipToPlay = impactSound.clip;
            Debug.Log("[CubeController] Usando sonido por defecto del AudioSource");
        }

        if (clipToPlay == null)
        {
            Debug.LogWarning("[CubeController] No hay sonidos de impacto asignados.");
            return;
        }

        // Randomizar pitch si está activado
        if (randomizePitch && impactSound != null)
        {
            impactSound.pitch = Random.Range(pitchRange.x, pitchRange.y);
        }

        // Reproducir el sonido
        impactSound.PlayOneShot(clipToPlay);

        // Restaurar el pitch original después de un breve momento
        if (randomizePitch && impactSound != null)
        {
            StartCoroutine(RestorePitchAfterDelay(clipToPlay.length));
        }
    }

    IEnumerator RestorePitchAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (impactSound != null)
        {
            impactSound.pitch = originalPitch;
        }
    }

    IEnumerator VibrateSoft()
    {
        Vibrate(vibrationDuration, false);
        yield return new WaitForSeconds(0.05f);
        Vibrate(vibrationDuration, false);
    }

    IEnumerator TestVibrationOnStart()
    {
        yield return new WaitForSeconds(0.25f);
        Debug.Log("[CubeController] Ejecutando vibracion de prueba al iniciar.");
        StartCoroutine(VibrateSoft());
    }

    void InitializeVibration()
    {
#if UNITY_ANDROID || UNITY_IOS
        Debug.Log("[CubeController] Vibracion inicializada con Handheld.Vibrate().");
#else
        Debug.Log("[CubeController] Vibracion nativa disponible solo en Android dispositivo.");
#endif
    }

    void Vibrate(long durationMs, bool isStrongVibration)
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        TriggerHandheldVibration(isStrongVibration);
#elif UNITY_ANDROID || UNITY_IOS
        Debug.Log("[CubeController] Prueba de vibracion omitida en Editor.");
#endif
    }

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
    void TriggerHandheldVibration(bool isStrongVibration)
    {
        Handheld.Vibrate();

        if (isStrongVibration)
        {
            StartCoroutine(RepeatHandheldVibration(0.08f, 2));
        }
    }

    IEnumerator RepeatHandheldVibration(float delay, int extraPulses)
    {
        for (int i = 0; i < extraPulses; i++)
        {
            yield return new WaitForSecondsRealtime(delay);
            Handheld.Vibrate();
        }
    }
#endif

    IEnumerator RestartLevelAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
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
        if (isDead) return;

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
        if (sceneController == null || !isGrounded) return;

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
        currentScale -= shrinkPerSpeed * speed * 600;


        if (currentScale <= minScale)
        {
            currentScale = minScale;
            isDead = true;
            OnCubeDied();
            RestartLevel();
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

    /// <summary>
    /// Reduce drásticamente el tamaño del cubo por contacto con una pared de fuego.
    /// Se quita una fracción de la escala ACTUAL (no de la escala inicial),
    /// así cada golpe duele más a medida que el cubo ya está pequeño.
    /// </summary>
    public void ApplyFireDamage(float fraction)
    {
        if (isDead || reachedGoal) return;

        // Quitamos la fracción de la escala ACTUAL
        float scaleToRemove = currentScale * fraction;
        currentScale -= scaleToRemove;

        // Si cae por debajo del mínimo, el cubo muere normalmente
        if (currentScale <= minScale)
        {
            currentScale = minScale;
            isDead = true;
            OnCubeDied();
            return;
        }

        // Aplicar el cambio visual inmediatamente
        transform.localScale = Vector3.one * currentScale;

        // Actualizar física con el nuevo tamaño
        UpdatePhysicsBasedOnSize();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  MUERTE
    // ─────────────────────────────────────────────────────────────────────
    void OnCubeDied()
    {
        rb.velocity = Vector3.zero;
        rb.isKinematic = true;
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
