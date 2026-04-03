using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inclina el escenario (tablero) usando el giroscopio del dispositivo móvil.
/// Expone BoardUp para que CubeController calcule la gravedad sobre la superficie.
///
/// Compatible con Unity 2017.4.40f1 LTS
///
/// ── POR QUÉ LAS VERSIONES ANTERIORES FALLABAN ───────────────────────────────
///
///  1. EULER ANGLES → GIMBAL LOCK + SALTOS:
///     Convertir el quaternion calibrado a Euler y leer .x / .z es inestable.
///     Unity resuelve Euler en orden YXZ, así que los tres ángulos son
///     interdependientes. En landscape, cerca de ±90° en un eje, los otros
///     dos saltan 180° (gimbal lock), causando tirones bruscos e impredecibles.
///
///  2. EJE LATERAL LENTO EN LANDSCAPE:
///     En landscape el dispositivo está girado ~90° respecto a portrait.
///     El movimiento "inclinar de lado" (roll físico) corresponde al pitch
///     del sensor y viceversa, y la magnitud angular disponible en cada eje
///     es diferente. Sin el remapeo correcto, un eje queda subdimensionado.
///
///  3. FILTRO COMPLEMENTARIO MAL UBICADO:
///     Se aplicaba el blend gyro/accel DESPUÉS de extraer Euler, sobre valores
///     ya distorsionados. Lo correcto es fusionar la ACTITUD completa
///     (quaternion) ANTES de extraer ningún ángulo.
///
/// ── SOLUCIÓN ─────────────────────────────────────────────────────────────────
///
///  • Filtro complementario directo sobre quaternions crudos.
///  • Extracción de tilt por PROYECCIÓN DE VECTORES, sin Euler:
///      Se expresa el vector mundo-arriba (0,1,0) en el espacio local del
///      sensor. Sus componentes X/Z son directamente el tilt. Inmune a
///      gimbal lock y sin saltos de ángulo.
///  • Remapeo de landscape: se permuta qué componente del sensor es
///      "adelante/atrás" y cuál es "izquierda/derecha".
///
/// SETUP:
///   - Adjuntar al GameObject raíz del escenario/tablero.
///   - El cubo DEBE ser hijo del escenario.
///   - Bloquear orientación en Landscape en Player Settings.
/// </summary>
public class GyroscopeSceneController : MonoBehaviour
{
    // ── Inclinación ──────────────────────────────────────────────────────────
    [Header("Inclinación")]
    [Tooltip("Ángulo máximo de inclinación del tablero en grados")]
    public float maxTiltAngle = 20f;

    [Tooltip("Sensibilidad del eje adelante/atrás (inclinar borde superior/inferior del móvil)")]
    [Range(0.5f, 5f)]
    public float sensitivityForward = 1.5f;

    [Tooltip("Sensibilidad del eje izquierda/derecha (inclinar bordes laterales del móvil)")]
    [Range(0.5f, 5f)]
    public float sensitivitySide = 1.5f;

    [Tooltip("Dead zone en grados — movimientos menores se ignoran para evitar temblor en reposo")]
    [Range(0f, 4f)]
    public float deadZone = 0.8f;

    [Tooltip("Landscape Left = botón home a la derecha. Landscape Right = botón home a la izquierda.")]
    public bool landscapeRight = false;

    // ── Respuesta ────────────────────────────────────────────────────────────
    [Header("Respuesta")]
    [Tooltip("Velocidad de seguimiento. Más alto = más directo e inmediato. Recomendado: 10-14.")]
    [Range(1f, 25f)]
    public float followSpeed = 12f;

    // ── Anti-deriva ──────────────────────────────────────────────────────────
    [Header("Anti-deriva (drift)")]
    [Tooltip("Cuánto corrige el acelerómetro la deriva. 0 = sin corrección, 0.05 = suave. No subir de 0.08.")]
    [Range(0f, 0.08f)]
    public float driftCorrection = 0.03f;

    [Tooltip("Suavizado del acelerómetro. Más alto = más estable pero más lento para corregir deriva.")]
    [Range(0.8f, 0.99f)]
    public float accelSmoothing = 0.92f;

    // ── Calibración ──────────────────────────────────────────────────────────
    [Header("Calibración")]
    [Tooltip("Segundos de espera antes de calibrar al inicio")]
    public float calibrationDelay = 1.5f;

    [Tooltip("Muestras a promediar durante la calibración (más = más preciso pero más lento)")]
    [Range(5, 30)]
    public int calibrationSamples = 20;

    // ── Propiedad pública ────────────────────────────────────────────────────
    /// <summary>Vector "arriba" del tablero en espacio mundo. Lo lee CubeController.</summary>
    public Vector3 BoardUp { get { return transform.up; } }

    // ── Estado privado ───────────────────────────────────────────────────────
    private Quaternion initialRotation;
    private Quaternion targetRotation;

    private Gyroscope gyro;
    private bool gyroAvailable;
    private bool calibrated;

    private Quaternion fusedAttitude = Quaternion.identity;
    private Quaternion calibrationOffset = Quaternion.identity;

    private Vector3 smoothAccel = new Vector3(0f, 0f, -1f);

    // ────────────────────────────────────────────────────────────────────────
    void Start()
    {
        initialRotation = transform.rotation;
        targetRotation = initialRotation;
        InitGyroscope();
    }

    void InitGyroscope()
    {
        if (SystemInfo.supportsGyroscope)
        {
            gyro = Input.gyro;
            gyro.enabled = true;
            gyro.updateInterval = 0.0083f; // solicitar ~120 Hz al hardware
            gyroAvailable = true;
            fusedAttitude = GyroToUnity(gyro.attitude);

            StartCoroutine(CalibrateRoutine(false));
            Debug.Log("[GyroController] Giroscopio activado.");
        }
        else
        {
            gyroAvailable = false;
            Debug.LogWarning("[GyroController] Giroscopio no disponible — usando mouse.");
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    /// <summary>Llamar desde un botón UI para recalibrar en runtime.</summary>
    public void Calibrate() { StartCoroutine(CalibrateRoutine(true)); }

    IEnumerator CalibrateRoutine(bool skipDelay)
    {
        calibrated = false;

        if (!skipDelay && calibrationDelay > 0f)
            yield return new WaitForSeconds(calibrationDelay);

        Quaternion avg = GyroToUnity(gyro.attitude);

        for (int i = 1; i < calibrationSamples; i++)
        {
            yield return new WaitForSeconds(0.016f);
            avg = Quaternion.Slerp(avg, GyroToUnity(gyro.attitude), 1f / (i + 1f));
        }

        calibrationOffset = Quaternion.Inverse(avg);
        fusedAttitude = Quaternion.identity;
        calibrated = true;
        Debug.Log("[GyroController] Calibrado.");
    }

    // ────────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (gyroAvailable)
            UpdateWithGyroscope();
        else
            UpdateWithMouse();

        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
    }

    void UpdateWithGyroscope()
    {
        if (!calibrated) return;

        // ── PASO 1: Actitud calibrada ────────────────────────────────────────
        Quaternion calibratedAttitude = calibrationOffset * GyroToUnity(gyro.attitude);

        // ── PASO 2: Filtro complementario sobre quaternions ──────────────────
        // Fusionamos la actitud del gyro (alta frecuencia, precisa pero deriva)
        // con la referencia del acelerómetro (baja frecuencia, no deriva).
        // Se opera sobre quaternions crudos, ANTES de extraer ángulos.
        Vector3 rawAccel = Input.acceleration;
        float accelMag = rawAccel.magnitude;
        bool accelOk = accelMag > 0.8f && accelMag < 1.2f;

        if (accelOk)
        {
            smoothAccel = Vector3.Lerp(rawAccel.normalized, smoothAccel, accelSmoothing);

            // Quaternion de referencia basado solo en la dirección de la gravedad
            Quaternion accelRef = Quaternion.FromToRotation(Vector3.back, smoothAccel);

            // El gyro domina; el accel corrige la deriva muy lentamente
            calibratedAttitude = Quaternion.Slerp(calibratedAttitude, accelRef, driftCorrection);
        }

        fusedAttitude = calibratedAttitude;

        // ── PASO 3: Extracción de tilt por proyección de vectores ────────────
        //
        // Expresamos el vector "arriba del mundo" en el espacio local del sensor.
        // Sus componentes X y Z dan el tilt sin Euler, sin gimbal lock, sin saltos.
        //
        //   worldUpInSensor.x > 0  → dispositivo inclinado hacia la derecha
        //   worldUpInSensor.z > 0  → dispositivo inclinado hacia atrás
        //
        Vector3 worldUpInSensor = Quaternion.Inverse(fusedAttitude) * Vector3.up;

        // En landscape el dispositivo está girado ~90° en Z respecto a portrait,
        // por lo que los ejes físicos del tablero están permutados en el sensor.
        float tiltForward;
        float tiltSide;

        if (!landscapeRight)
        {
            // Landscape Left (home button a la derecha):
            tiltForward = -worldUpInSensor.z * sensitivityForward;
            tiltSide = -worldUpInSensor.x * sensitivitySide;
        }
        else
        {
            // Landscape Right (home button a la izquierda):
            tiltForward = worldUpInSensor.z * sensitivityForward;
            tiltSide = worldUpInSensor.x * sensitivitySide;
        }

        // Convertir de componente de seno a grados reales con asin
        tiltForward = Mathf.Asin(Mathf.Clamp(tiltForward, -1f, 1f)) * Mathf.Rad2Deg;
        tiltSide = Mathf.Asin(Mathf.Clamp(tiltSide, -1f, 1f)) * Mathf.Rad2Deg;

        // ── PASO 4: Dead zone con transición suave ───────────────────────────
        tiltForward = ApplyDeadZone(tiltForward, deadZone);
        tiltSide = ApplyDeadZone(tiltSide, deadZone);

        // ── PASO 5: Clamp y construir rotación objetivo ──────────────────────
        tiltForward = Mathf.Clamp(tiltForward, -maxTiltAngle, maxTiltAngle);
        tiltSide = Mathf.Clamp(tiltSide, -maxTiltAngle, maxTiltAngle);

        targetRotation = initialRotation * Quaternion.Euler(tiltForward, 0f, tiltSide);
    }

    // Dead zone con salida suave (sin escalón abrupto al entrar/salir)
    float ApplyDeadZone(float value, float zone)
    {
        if (zone <= 0f) return value;
        if (Mathf.Abs(value) < zone) return 0f;
        return value - Mathf.Sign(value) * zone;
    }

    void UpdateWithMouse()
    {
        if (!Input.GetMouseButton(0)) return;

        float mx = Input.GetAxis("Mouse X") * sensitivitySide * 3f;
        float my = -Input.GetAxis("Mouse Y") * sensitivityForward * 3f;

        Vector3 cur = targetRotation.eulerAngles;
        float newX = Mathf.Clamp(NormalizeAngle(cur.x) - my, -maxTiltAngle, maxTiltAngle);
        float newZ = Mathf.Clamp(NormalizeAngle(cur.z) - mx, -maxTiltAngle, maxTiltAngle);

        targetRotation = initialRotation * Quaternion.Euler(newX, 0f, newZ);
    }

    // ────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Convierte la actitud del giroscopio al sistema de coordenadas de Unity.
    /// El sensor usa Right-Hand System; Unity usa Left-Hand System.
    /// Conversión estándar documentada por Unity para iOS y Android.
    /// </summary>
    Quaternion GyroToUnity(Quaternion q)
    {
        return new Quaternion(q.x, q.y, -q.z, -q.w);
    }

    float NormalizeAngle(float a)
    {
        return a > 180f ? a - 360f : a;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.up * 2f);
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.right * 2f);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
    }
}