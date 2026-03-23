using System.Collections;
using System.Collections.Generic;

using UnityEngine;

/// <summary>
/// Inclina el escenario (tablero) usando el giroscopio del dispositivo móvil.
/// Expone BoardUp para que CubeController calcule la gravedad sobre la superficie.
///
/// Compatible con Unity 2017.4.40f1 LTS
///
/// SETUP:
///   - Adjuntar este script al GameObject raíz del escenario/tablero.
///   - El cubo DEBE ser hijo del escenario.
///   - El tablero debe tener un BoxCollider en su superficie.
/// </summary>
public class GyroscopeSceneController : MonoBehaviour
{
    [Header("Inclinación")]
    [Tooltip("Ángulo máximo de inclinación en grados (recomendado: 15-25)")]
    public float maxTiltAngle = 20f;

    [Tooltip("Suavidad del movimiento — más alto = más suave pero más lento")]
    public float smoothSpeed = 5f;

    [Tooltip("Multiplicador de sensibilidad del giroscopio")]
    public float gyroSensitivity = 1.5f;

    // ── Propiedad pública que CubeController lee cada frame ──────────────
    // Devuelve el vector "arriba" del tablero en espacio mundo.
    // CubeController proyecta la gravedad sobre este plano para mover el cubo.
    public Vector3 BoardUp { get { return transform.up; } }

    private Quaternion targetRotation;
    private Quaternion initialRotation;
    private Gyroscope gyro;
    private bool gyroAvailable = false;
    private Quaternion gyroCalibrationOffset = Quaternion.identity;

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
            gyroAvailable = true;
            Calibrate();
            Debug.Log("[GyroController] Giroscopio activado.");
        }
        else
        {
            gyroAvailable = false;
            Debug.LogWarning("[GyroController] Giroscopio no disponible — usando mouse como fallback.");
        }
    }

    /// <summary>
    /// Fija la orientación actual como posición neutra (posición cero).
    /// Puede ser llamado desde un botón UI en runtime.
    /// </summary>
    public void Calibrate()
    {
        if (gyroAvailable)
            gyroCalibrationOffset = Quaternion.Inverse(GyroToUnity(gyro.attitude));
    }

    void Update()
    {
        if (gyroAvailable)
            UpdateWithGyroscope();
        else
            UpdateWithMouse();

        // Aplicar la rotación suavizada al tablero
        transform.rotation = Quaternion.Slerp(
            transform.rotation, targetRotation, Time.deltaTime * smoothSpeed);
    }

    void UpdateWithGyroscope()
    {
        Quaternion rawGyro = GyroToUnity(gyro.attitude);
        Quaternion calibratedGyro = gyroCalibrationOffset * rawGyro;
        Vector3 euler = calibratedGyro.eulerAngles;

        // Solo inclinación X (adelante/atrás) y Z (lateral) — descartamos Y
        float tiltX = Mathf.Clamp(
            NormalizeAngle(euler.x) * gyroSensitivity, -maxTiltAngle, maxTiltAngle);
        float tiltZ = Mathf.Clamp(
            NormalizeAngle(euler.z) * gyroSensitivity, -maxTiltAngle, maxTiltAngle);

        targetRotation = initialRotation * Quaternion.Euler(tiltX, 0f, tiltZ);
    }

    void UpdateWithMouse()
    {
        // Fallback para el editor Unity — clic izquierdo + arrastrar el mouse
        if (Input.GetMouseButton(0))
        {
            float mx = Input.GetAxis("Mouse X") * gyroSensitivity * 2f;
            float my = Input.GetAxis("Mouse Y") * gyroSensitivity * 2f;
            Vector3 cur = targetRotation.eulerAngles;

            float newX = Mathf.Clamp(NormalizeAngle(cur.x) - my, -maxTiltAngle, maxTiltAngle);
            float newZ = Mathf.Clamp(NormalizeAngle(cur.z) - mx, -maxTiltAngle, maxTiltAngle);

            targetRotation = initialRotation * Quaternion.Euler(newX, 0f, newZ);
        }
    }

    /// <summary>
    /// Convierte la actitud del giroscopio al sistema de coordenadas de Unity.
    /// El giroscopio usa ejes distintos que hay que remapear.
    /// </summary>
    Quaternion GyroToUnity(Quaternion q)
    {
        return new Quaternion(q.x, q.y, -q.z, -q.w);
    }

    float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }

    void OnGUI()
    {
        if (GUI.Button(new Rect(10, 10, 130, 40), "Calibrar Gyro"))
            Calibrate();

        Vector3 tilt = transform.rotation.eulerAngles;
        GUI.Label(new Rect(10, 58, 250, 20),
            string.Format("Inclinación  X:{0:F1}°  Z:{1:F1}°",
                NormalizeAngle(tilt.x), NormalizeAngle(tilt.z)));
    }
}