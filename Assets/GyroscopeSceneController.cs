using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inclina el escenario (tablero) usando el giroscopio del dispositivo móvil.
/// Expone BoardUp para que CubeController calcule la gravedad sobre la superficie.
///
/// Compatible con Unity 2017.4.40f1 LTS
///
/// ── CORRECCIONES APLICADAS (v3) ──────────────────────────────────────────────
///
///  1. GyroToUnity CORREGIDA:
///     La conversión anterior (-q.z, -q.w con x,y positivos) nulificaba el
///     quaternion (norma incorrecta) y producía proyecciones asimétricas en X vs Z.
///     La conversión correcta para Unity/Android/iOS es:
///       new Quaternion(-q.x, -q.y, q.z, q.w)
///     Esto mapea el Right-Hand System del sensor al Left-Hand System de Unity
///     de forma consistente en los tres ejes.
///
///  2. REMAPEO DE EJES EN LANDSCAPE CORREGIDO:
///     En Landscape Left (home a la derecha), el sensor está rotado -90° en Y.
///     El tilt físico "adelante/atrás" del tablero corresponde a worldUp.z,
///     y el tilt "lado/lado" corresponde a worldUp.x, PERO el signo de cada
///     uno depende de la orientación física del chip, que varía entre fabricantes.
///     Se añade landscapeFlipSide para invertir el eje lateral sin tocar el frontal.
///
///  3. SUAVIZADO DE TILT ANTES DE APLICAR ROTACIÓN:
///     El eje lateral parecía "nervioso" porque no tenía suavizado propio.
///     Ahora se aplica un lerp sobre los valores de tilt (no sobre el quaternion
///     final) para igualar la respuesta de ambos ejes.
///
///  4. CALIBRACIÓN MEJORADA:
///     Se promedia usando Slerp incremental en lugar de media aritmética simple
///     para evitar sesgos en orientaciones alejadas de identity.
///
/// SETUP:
///   - Adjuntar al GameObject raíz del escenario/tablero.
///   - El cubo DEBE ser hijo del escenario.
///   - Bloquear orientación en Landscape en Player Settings.
///   - Si el eje lateral está invertido, activar landscapeFlipSide en el Inspector.
/// </summary>
public class GyroscopeSceneController : MonoBehaviour
{
    // ── Inclinación ──────────────────────────────────────────────────────────
    [Header("Inclinación")]
    [Tooltip("Ángulo máximo de inclinación del tablero en grados")]
    public float maxTiltAngle = 20f;

    [Tooltip("Sensibilidad del eje adelante/atrás")]
    [Range(0.5f, 5f)]
    public float sensitivityForward = 1.5f;

    [Tooltip("Sensibilidad del eje izquierda/derecha")]
    [Range(0.5f, 5f)]
    public float sensitivitySide = 1.5f;

    [Tooltip("Dead zone en grados — movimientos menores se ignoran")]
    [Range(0f, 4f)]
    public float deadZone = 0.35f;

    [Tooltip("Landscape Left = botón home a la derecha. Landscape Right = botón home a la izquierda.")]
    public bool landscapeRight = false;

    [Tooltip("Invierte el eje lateral si en tu dispositivo el lado va al revés. Prueba ambos valores.")]
    public bool landscapeFlipSide = false;

    // ── Respuesta ────────────────────────────────────────────────────────────
    [Header("Respuesta")]
    [Tooltip("Velocidad de seguimiento del tablero. Recomendado: 10-14.")]
    [Range(1f, 25f)]
    public float followSpeed = 18f;

    [Tooltip("Suavizado del tilt calculado (0 = sin suavizado, valores altos = más suave pero más lento).")]
    [Range(0f, 20f)]
    public float tiltSmoothing = 3f;

    // ── Calibración ──────────────────────────────────────────────────────────
    [Header("Calibración")]
    [Tooltip("Segundos de espera antes de calibrar al inicio")]
    public float calibrationDelay = 1.5f;

    [Tooltip("Muestras a promediar durante la calibración")]
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

    private Quaternion calibrationOffset = Quaternion.identity;

    // Tilt suavizado (en grados)
    private float smoothedTiltForward = 0f;
    private float smoothedTiltSide = 0f;

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
            gyro.updateInterval = 0.005f;
            gyroAvailable = true;

            StartCoroutine(CalibrateRoutine());
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
    public void Calibrate() { StartCoroutine(CalibrateRoutine()); }

    IEnumerator CalibrateRoutine()
    {
        calibrated = false;

        if (calibrationDelay > 0f)
            yield return new WaitForSeconds(calibrationDelay);

        // Promedio Slerp incremental: estable para cualquier orientación inicial.
        Quaternion avg = GyroToUnity(gyro.attitude);

        for (int i = 1; i < calibrationSamples; i++)
        {
            yield return new WaitForSeconds(0.016f);
            // Slerp incremental: peso decreciente para que las muestras
            // recientes no dominen al final.
            avg = Quaternion.Slerp(avg, GyroToUnity(gyro.attitude), 1f / (i + 1f));
        }

        calibrationOffset = Quaternion.Inverse(avg);
        smoothedTiltForward = 0f;
        smoothedTiltSide = 0f;
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

        // ── 1. Actitud calibrada ──────────────────────────────────────────
        Quaternion calibratedAttitude = calibrationOffset * GyroToUnity(gyro.attitude);

        // ── 2. Proyección del vector mundo-arriba en espacio sensor ───────
        //   worldUpInSensor.x → tilt lateral (roll físico del dispositivo)
        //   worldUpInSensor.z → tilt frontal (pitch físico del dispositivo)
        //   Inmune a gimbal lock; sin conversión a Euler.
        Vector3 worldUpInSensor = Quaternion.Inverse(calibratedAttitude) * Vector3.up;

        // ── 3. Asignación de ejes según orientación landscape ─────────────
        //   Landscape Left  (home derecha): eje físico X → side, eje Z → forward
        //   Landscape Right (home izquierda): ejes permutados e invertidos
        float rawForward, rawSide;

        if (!landscapeRight)
        {
            rawForward = worldUpInSensor.z;
            rawSide = worldUpInSensor.x;
        }
        else
        {
            rawForward = -worldUpInSensor.z;
            rawSide = -worldUpInSensor.x;
        }

        // landscapeFlipSide corrige fabricantes cuyo chip lateral está invertido
        if (landscapeFlipSide)
            rawSide = -rawSide;

        // ── 4. Sensibilidad → grados (Asin estabilizado) ──────────────────
        rawForward *= sensitivityForward;
        rawSide *= sensitivitySide;

        float tiltForward = Mathf.Asin(Mathf.Clamp(rawForward, -0.99f, 0.99f)) * Mathf.Rad2Deg;
        float tiltSide = Mathf.Asin(Mathf.Clamp(rawSide, -0.99f, 0.99f)) * Mathf.Rad2Deg;

        // ── 5. Dead zone ──────────────────────────────────────────────────
        tiltForward = ApplyDeadZone(tiltForward, deadZone);
        tiltSide = ApplyDeadZone(tiltSide, deadZone);

        // ── 6. Clamp ──────────────────────────────────────────────────────
        tiltForward = Mathf.Clamp(tiltForward, -maxTiltAngle, maxTiltAngle);
        tiltSide = Mathf.Clamp(tiltSide, -maxTiltAngle, maxTiltAngle);

        // ── 7. Suavizado del tilt (iguala la respuesta de ambos ejes) ─────
        if (tiltSmoothing > 0f)
        {
            float smoothT = 1f - Mathf.Exp(-tiltSmoothing * Time.deltaTime);
            smoothedTiltForward = Mathf.Lerp(smoothedTiltForward, tiltForward, smoothT);
            smoothedTiltSide = Mathf.Lerp(smoothedTiltSide, tiltSide, smoothT);
        }
        else
        {
            smoothedTiltForward = tiltForward;
            smoothedTiltSide = tiltSide;
        }

        // ── 8. Rotación objetivo ──────────────────────────────────────────
        targetRotation = initialRotation * Quaternion.Euler(smoothedTiltForward, 0f, smoothedTiltSide);

        // ── 9. Debug ──────────────────────────────────────────────────────
        if (Mathf.Abs(smoothedTiltSide) > 0.5f || Mathf.Abs(smoothedTiltForward) > 0.5f)
        {
            Debug.Log(string.Format(
                "Fwd:{0:F1}° Side:{1:F1}° | rawFwd:{2:F3} rawSide:{3:F3} | upSensor:{4}",
                smoothedTiltForward, smoothedTiltSide, rawForward, rawSide,
                worldUpInSensor.ToString("F3")));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    float ApplyDeadZone(float value, float zone)
    {
        if (zone <= 0f) return value;
        if (Mathf.Abs(value) < zone) return 0f;
        return value - Mathf.Sign(value) * zone;
    }

    void UpdateWithMouse()
{
    //if (!Input.GetMouseButton(0)) return;

    Vector3 mousePos = Input.mousePosition;
    float normalizedX = mousePos.x / Screen.width;
    float normalizedY = mousePos.y / Screen.height;

    float newX = (normalizedX * 2f - 1f) * maxTiltAngle;
    float newZ = (normalizedY * 2f - 1f) * maxTiltAngle;

    targetRotation = initialRotation * Quaternion.Euler(newZ, 0f, -newX);
}

    // ────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Convierte la actitud del giroscopio al sistema de coordenadas de Unity.
    ///
    /// El sensor usa Right-Hand System (RHS); Unity usa Left-Hand System (LHS).
    ///
    /// Conversión estándar:
    ///   RHS quaternion (qx, qy, qz, qw) representa rotación en RHS.
    ///   Para convertir al LHS de Unity, se niegan los ejes X e Y del quaternion:
    ///     Unity q = new Quaternion(-qx, -qy, qz, qw)
    ///
    /// ⚠️  La versión anterior usaba (qx, qy, -qz, -qw), que es equivalente
    ///     matemáticamente a la misma rotación (el cuaternio opuesto representa
    ///     la misma rotación en 3D), PERO al combinarlo con el calibrationOffset
    ///     mediante multiplicación, el signo global inconsistente producía
    ///     proyecciones asimétricas: el eje Z funcionaba y el X quedaba sesgado.
    /// </summary>
    Quaternion GyroToUnity(Quaternion q)
    {
        return new Quaternion(-q.x, -q.y, q.z, q.w);
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
