using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Sonido de Fuego")]
    [Tooltip("AudioSource exclusivo para el sonido al tocar fuego (descongelamiento)")]
    public AudioSource fireSound;

    [Tooltip("Clips de sonido de fuego (seleccionará uno aleatorio si hay varios)")]
    public AudioClip[] fireSounds;

    [Tooltip("Si está activado, varía ligeramente el pitch del sonido de fuego")]
    public bool randomizeFirePitch = true;

    [Tooltip("Rango de variación del pitch para fuego (mínimo y máximo)")]
    public Vector2 firePitchRange = new Vector2(0.9f, 1.1f);

    [Tooltip("Volumen del sonido de fuego (1 = volumen del AudioSource)")]
    [Range(0f, 1f)]
    public float fireVolume = 1f;

    [Header("Sonido de Rotura")]
    [Tooltip("AudioSource exclusivo para el sonido de rompimiento del bloque (debe tener iceCubeCrushing asignado)")]
    public AudioSource iceCubeCrushingSound;

    [Tooltip("Clips de sonido de rotura (seleccionará uno aleatorio si hay varios)")]
    public AudioClip[] breakSounds;

    [Tooltip("Si está activado, varía ligeramente el pitch del sonido de rotura")]
    public bool randomizeBreakPitch = true;

    [Tooltip("Rango de variación del pitch para rotura (mínimo y máximo)")]
    public Vector2 breakPitchRange = new Vector2(0.7f, 1.3f);

    [Tooltip("Volumen del sonido de rotura (1 = volumen del AudioSource)")]
    [Range(0f, 1f)]
    public float breakVolume = 1f;

    [Tooltip("Si está activado, varía ligeramente el pitch del sonido de impacto")]
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
    private GameObject gameOverCanvas;
    private float currentScale;
    private float totalShrinkRange;
    private bool isDead = false;
    private bool isGrounded = false;
    public bool IsGrounded { get { return isGrounded; } }
    private Vector3 lastDebugForce = Vector3.zero;
    private GyroscopeSceneController sceneController;
    private Transform boardTransform;
    private float lastImpactFeedbackTime = float.NegativeInfinity;
    private float originalPitch;
    private float originalBreakPitch;
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

        CreateGameOverUI();
        SetupRigidbody();
        ConfigureImpactSound();
        ConfigureBreakSound();
        ConfigureFireSound();

        // Buscar el controlador automáticamente
        if (boardController == null && transform.parent != null)
        {
            sceneController = transform.parent.GetComponent<GyroscopeSceneController>();
            if (sceneController == null)
            {
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

    // ─────────────────────────────────────────────────────────────────────
    //  CONFIGURACIÓN DE AUDIO
    // ─────────────────────────────────────────────────────────────────────
    void ConfigureImpactSound()
    {
        // Si no está asignado manualmente, buscar el primer AudioSource del objeto
        if (impactSound == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length > 0)
                impactSound = sources[0];
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
        originalPitch = impactSound.pitch;
    }

    void ConfigureBreakSound()
    {
        // Si no está asignado manualmente, buscar un segundo AudioSource distinto al de impacto
        if (iceCubeCrushingSound == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            foreach (AudioSource src in sources)
            {
                if (src != impactSound)
                {
                    iceCubeCrushingSound = src;
                    break;
                }
            }
        }

        // Fallback: buscar en hijos (distinto al de impacto)
        if (iceCubeCrushingSound == null)
        {
            AudioSource[] childSources = GetComponentsInChildren<AudioSource>(true);
            foreach (AudioSource src in childSources)
            {
                if (src != impactSound)
                {
                    iceCubeCrushingSound = src;
                    break;
                }
            }
        }

        if (iceCubeCrushingSound == null)
        {
            Debug.LogWarning("[CubeController] No se encontró un AudioSource exclusivo para rotura en " + gameObject.name + ". Asigna 'iceCubeCrushingSound' manualmente en el Inspector.");
            return;
        }

        iceCubeCrushingSound.playOnAwake = false;
        iceCubeCrushingSound.loop = false;
        originalBreakPitch = iceCubeCrushingSound.pitch;

        Debug.Log("[CubeController] AudioSource de rotura configurado: " + iceCubeCrushingSound.gameObject.name);
    }

    void ConfigureFireSound()
    {
        if (fireSound == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            foreach (AudioSource src in sources)
            {
                if (src != impactSound && src != iceCubeCrushingSound)
                {
                    fireSound = src;
                    break;
                }
            }
        }

        if (fireSound == null)
        {
            AudioSource[] childSources = GetComponentsInChildren<AudioSource>(true);
            foreach (AudioSource src in childSources)
            {
                if (src != impactSound && src != iceCubeCrushingSound)
                {
                    fireSound = src;
                    break;
                }
            }
        }

        if (fireSound == null)
        {
            Debug.LogWarning("[CubeController] No se encontró AudioSource para fuego en " + gameObject.name + ". Asigna 'fireSound' manualmente en el Inspector.");
            return;
        }

        fireSound.playOnAwake = false;
        fireSound.loop = false;
        Debug.Log("[CubeController] AudioSource de fuego configurado: " + fireSound.gameObject.name);
    }

    void PlayFireSound()
    {
        if (fireSound == null)
        {
            ConfigureFireSound();
            if (fireSound == null) return;
        }

        AudioClip clipToPlay = null;

        if (fireSounds != null && fireSounds.Length > 0)
        {
            List<AudioClip> validClips = new List<AudioClip>();
            foreach (AudioClip clip in fireSounds)
            {
                if (clip != null) validClips.Add(clip);
            }

            if (validClips.Count > 0)
            {
                clipToPlay = validClips[Random.Range(0, validClips.Count)];
                Debug.Log("[CubeController] Reproduciendo clip de fuego aleatorio: " + clipToPlay.name);
            }
        }

        if (clipToPlay == null && fireSound.clip != null)
        {
            clipToPlay = fireSound.clip;
            Debug.Log("[CubeController] Usando clip del AudioSource de fuego: " + clipToPlay.name);
        }

        if (clipToPlay == null)
        {
            Debug.LogWarning("[CubeController] fireSound no tiene ningún AudioClip asignado.");
            return;
        }

        if (randomizeFirePitch)
        {
            fireSound.pitch = Random.Range(firePitchRange.x, firePitchRange.y);
        }

        fireSound.PlayOneShot(clipToPlay, fireVolume);
        Debug.Log("[CubeController] Sonido de fuego reproducido: " + clipToPlay.name);
    }

    void PlayBreakSound()
    {
        if (iceCubeCrushingSound == null)
        {
            ConfigureBreakSound();
            if (iceCubeCrushingSound == null)
            {
                Debug.LogWarning("[CubeController] No hay AudioSource de rotura disponible.");
                return;
            }
        }

        // Elegir clip: primero del array breakSounds, luego el clip del AudioSource
        AudioClip clipToPlay = null;

        if (breakSounds != null && breakSounds.Length > 0)
        {
            List<AudioClip> validClips = new List<AudioClip>();
            foreach (AudioClip clip in breakSounds)
            {
                if (clip != null) validClips.Add(clip);
            }

            if (validClips.Count > 0)
            {
                clipToPlay = validClips[Random.Range(0, validClips.Count)];
                Debug.Log("[CubeController] Reproduciendo clip de rotura aleatorio: " + clipToPlay.name);
            }
        }

        if (clipToPlay == null && iceCubeCrushingSound.clip != null)
        {
            clipToPlay = iceCubeCrushingSound.clip;
            Debug.Log("[CubeController] Usando clip del AudioSource de rotura: " + clipToPlay.name);
        }

        if (clipToPlay == null)
        {
            Debug.LogWarning("[CubeController] iceCubeCrushingSound no tiene ningún AudioClip asignado. Asigna 'iceCubeCrushing' en el Inspector.");
            return;
        }

        // Variación de pitch
        if (randomizeBreakPitch)
        {
            iceCubeCrushingSound.pitch = Random.Range(breakPitchRange.x, breakPitchRange.y);
            Debug.Log("[CubeController] Pitch de rotura ajustado a: " + iceCubeCrushingSound.pitch);
        }

        iceCubeCrushingSound.PlayOneShot(clipToPlay, breakVolume);

        // Restaurar pitch tras la reproducción
        if (randomizeBreakPitch)
        {
            StartCoroutine(RestoreBreakPitchAfterDelay(clipToPlay.length));
        }

        Debug.Log("[CubeController] Sonido de rotura reproducido: " + clipToPlay.name);
    }

    IEnumerator RestoreBreakPitchAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (iceCubeCrushingSound != null)
        {
            iceCubeCrushingSound.pitch = originalBreakPitch;
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

        if (impactForce >= feedbackThreshold)
        {
            TriggerImpactFeedback();
        }

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

        PlayBreakSound();
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

        gameOverCanvas.SetActive(true);
        Vibrate(200, true);
        //RestartLevel();
    }

    void TriggerImpactFeedback()
    {
        if (isDead) return;
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

        AudioClip clipToPlay = null;

        if (impactSounds != null && impactSounds.Length > 0)
        {
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

        if (randomizePitch && impactSound != null)
        {
            impactSound.pitch = Random.Range(pitchRange.x, pitchRange.y);
        }

        impactSound.PlayOneShot(clipToPlay);

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
        gameOverCanvas.SetActive(true);
        //RestartLevel();
    }

    void RestartLevel()
    {
        gameOverCanvas.SetActive(false);
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

    void ApplySurfaceGravity()
    {
        if (sceneController == null) return;

        Vector3 boardNormal = sceneController.BoardUp;
        Vector3 gravity = Vector3.down * gravityScale;

        Vector3 gravityOnSurface = gravity - Vector3.Dot(gravity, boardNormal) * boardNormal;

        lastDebugForce = gravityOnSurface;

        if (isGrounded)
        {
            rb.AddForce(gravityOnSurface, ForceMode.Acceleration);
        }
        else
        {
            rb.AddForce(Vector3.down * gravityScale, ForceMode.Acceleration);
        }
    }

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
    //  UPDATE
    // ─────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (isDead) return;

        AlignToBoardSurface();
        Shrink();
        UpdatePhysicsBasedOnSize();
    }

    void AlignToBoardSurface()
    {
        if (sceneController == null || !isGrounded) return;

        Quaternion targetRotation = sceneController.transform.rotation;

        transform.rotation = Quaternion.Slerp(
            transform.rotation, targetRotation, Time.deltaTime * alignmentSpeed);
    }

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
            //RestartLevel();
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

    void ClampPositionToBoard()
    {
        if (transform.parent == null) return;

        Vector3 localPos = transform.localPosition;
        Vector3 localVel = transform.parent.InverseTransformDirection(rb.velocity);
        float halfCube = currentScale * 0.5f;
        float softZone = 0.3f;
        bool hitWall = false;

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
            float t = (localPos.x - (limitX - softZone)) / softZone;
            localVel.x -= localVel.x * t * 0.15f;
        }
        else if (localPos.x < -(limitX - softZone) && localVel.x < 0)
        {
            float t = (-localPos.x - (limitX - softZone)) / softZone;
            localVel.x -= localVel.x * t * 0.15f;
        }

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

    public void ApplyFireDamage(float fraction)
    {
        if (isDead || reachedGoal) return;

        float scaleToRemove = currentScale * fraction;
        currentScale -= scaleToRemove;

        PlayFireSound();

        if (currentScale <= minScale)
        {
            currentScale = minScale;
            isDead = true;
            OnCubeDied();
            return;
        }

        transform.localScale = Vector3.one * currentScale;
        UpdatePhysicsBasedOnSize();
    }

    void OnCubeDied()
    {
        rb.velocity = Vector3.zero;
        rb.isKinematic = true;
        gameOverCanvas.SetActive(true);
        Time.timeScale = 0f; // Opcional: pausar el juego
    }

    void DeactivateCube()
    {
        gameObject.SetActive(false);
    }

    float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }

    void CreateGameOverUI()
    {
        // Canvas
        GameObject canvasGO = new GameObject("Canvas_GameOver");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // Mismo orden que pausa

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();

        // =========================
        // 🔳 FONDO OSCURO
        // =========================
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);

        Image bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.8f); // Negro semi-transparente

        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // =========================
        // 📝 TEXTO "GAME OVER"
        // =========================
        GameObject textGO = new GameObject("Texto_GameOver");
        textGO.transform.SetParent(canvasGO.transform, false);

        Text gameOverText = textGO.AddComponent<Text>();
        gameOverText.text = "GAME OVER";
        gameOverText.fontSize = 80;
        gameOverText.alignment = TextAnchor.MiddleCenter;
        gameOverText.color = Color.white;
        gameOverText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        gameOverText.fontStyle = FontStyle.Bold;

        // Sombra (opcional pero recomendada)
        Shadow shadow = textGO.AddComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(2, -2);

        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.6f);
        textRect.anchorMax = new Vector2(0.5f, 0.6f);
        textRect.sizeDelta = new Vector2(800, 150);
        textRect.anchoredPosition = Vector2.zero;

        // =========================
        // 🔘 BOTONES
        // =========================
        // Botón Reintentar (mismo que en pausa)
        CreateButton(canvasGO.transform, "Reintentar", new Vector2(0, -50), RestartLevel);
        // Botón Menú Principal
        CreateButton(canvasGO.transform, "Menú Principal", new Vector2(0, -150), GoToMainMenu);

        // Oculto al inicio
        canvasGO.SetActive(false);

        // Guardamos referencia (opcional, si la necesitas para mostrar/ocultar)
        gameOverCanvas = canvasGO;
    }
    void CreateButton(Transform parent, string buttonText, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonGO = new GameObject("Btn_" + buttonText);
        buttonGO.transform.SetParent(parent, false);

        // Imagen del botón
        Image btnImage = buttonGO.AddComponent<Image>();
        btnImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // Componente Button
        Button button = buttonGO.AddComponent<Button>();
        button.onClick.AddListener(action);

        // Transición de colores
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        colors.highlightedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        colors.pressedColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        button.colors = colors;

        RectTransform btnRect = buttonGO.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = new Vector2(300, 60);
        btnRect.anchoredPosition = position;

        // Texto del botón
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);

        Text text = textGO.AddComponent<Text>();
        text.text = buttonText;
        text.fontSize = 30;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontStyle = FontStyle.Bold;

        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }
    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        #if UNITY_5_3_OR_NEWER
            UnityEngine.SceneManagement.SceneManager.LoadScene("mainscene");
        #else
            Application.LoadLevel("mainscene");
        #endif
    }
}