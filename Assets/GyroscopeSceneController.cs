using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Inclina el escenario (tablero) usando el giroscopio del dispositivo móvil.
/// Expone BoardUp para que CubeController calcule la gravedad sobre la superficie.
///
/// Compatible con Unity 2017.4.40f1 LTS
///
/// ── CAMBIOS v4 ──────────────────────────────────────────────────────────────
///
///  1. CALIBRACIÓN MÁS ESTABLE:
///     Se descarta el primer 30% de muestras (warm-up del sensor) y se usa
///     promedio Slerp con peso uniforme sobre las muestras restantes, evitando
///     el sesgo que introducía el promedio incremental 1/(i+1) en v3.
///
///  2. DEAD ZONE SUAVE (Soft Dead Zone):
///     Reemplaza el corte abrupto por una zona de transición gradual.
///     Dentro de la zona muerta → 0. Entre zona muerta y zona viva → rampa
///     suave (smoothstep). Elimina el "salto" al salir de la zona muerta.
///
///  3. MAPEO CIRCULAR DE EJES (Radial Remapping):
///     Se convierte el par (tiltForward, tiltSide) a coordenadas polares,
///     se aplica la dead zone sobre la magnitud total (no por eje), y se
///     re-proyecta. Esto hace que las esquinas sean tan alcanzables como los
///     ejes puros: inclinación en diagonal llega a la magnitud máxima sin
///     que un eje "robe" al otro.
///
///  4. CURVA DE RESPUESTA CONFIGURABLE (Response Curve):
///     Después del remapeo radial, se eleva la magnitud normalizada a
///     responseCurve (exponente). Con 1.0 la respuesta es lineal. Con 0.6-0.8
///     los movimientos pequeños son más sensibles (útil para ajuste fino).
///     Con 1.2-1.5 los movimientos grandes dominan (más dramático).
///
///  5. SUAVIZADO UNIFICADO:
///     Se elimina la doble suavización que existía (tiltSmoothing + followSpeed)
///     y se reemplaza por un único lerp exponencial sobre el vector de tilt 2D.
///     followSpeed sigue controlando la rotación física del tablero.
///
///  6. BLOQUEO TÁCTIL DUAL MEJORADO:
///     En móvil, se deben presionar ambos botones laterales simultáneamente
///     para permitir el movimiento del giroscopio. Los botones se crean
///     automáticamente con UI en ScreenSpaceOverlay.
///
/// SETUP:
///   - Adjuntar al GameObject raíz del escenario/tablero.
///   - El cubo DEBE ser hijo del escenario.
///   - Bloquear orientación en Landscape en Player Settings.
///   - Si el eje lateral está invertido, activar landscapeFlipSide en el Inspector.
///   - En móvil, se crearán automáticamente dos botones laterales.
///   - En PC/Editor, usar teclas Q (izquierdo) y P (derecho) para simular.
///
/// VALORES RECOMENDADOS PARA EMPEZAR:
///   maxTiltAngle      = 20
///   sensitivityForward = 1.5 | sensitivitySide = 1.5
///   deadZoneInner     = 0.5  | deadZoneOuter   = 2.0
///   responseCurve     = 0.75
///   tiltSmoothing     = 8
///   followSpeed       = 18
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

    [Header("Dead Zone Suave")]
    [Tooltip("Zona muerta interna en grados — por debajo de este valor NO hay movimiento")]
    [Range(0f, 5f)]
    public float deadZoneInner = 0.5f;

    [Tooltip("Zona de transición en grados — entre inner y outer la respuesta sube suavemente (smoothstep)")]
    [Range(0f, 5f)]
    public float deadZoneOuter = 2.0f;

    [Tooltip("Landscape Left = botón home a la derecha. Landscape Right = botón home a la izquierda.")]
    public bool landscapeRight = false;

    [Tooltip("Invierte el eje lateral si en tu dispositivo el lado va al revés.")]
    public bool landscapeFlipSide = false;

    // ── Respuesta ────────────────────────────────────────────────────────────
    [Header("Respuesta")]
    [Tooltip("Velocidad de seguimiento del tablero. Recomendado: 10-18.")]
    [Range(1f, 25f)]
    public float followSpeed = 18f;

    [Tooltip("Suavizado del tilt calculado. Valores altos = más suave pero más lento. Recomendado: 6-10.")]
    [Range(0f, 20f)]
    public float tiltSmoothing = 8f;

    [Tooltip("Curva de respuesta (exponente). 1.0 = lineal. <1 = más sensible en el centro. >1 = más dramático en extremos. Recomendado: 0.75")]
    [Range(0.3f, 2f)]
    public float responseCurve = 0.75f;

    // ── Calibración ──────────────────────────────────────────────────────────
    [Header("Calibración")]
    [Tooltip("Segundos de espera antes de calibrar al inicio")]
    public float calibrationDelay = 1.5f;

    [Tooltip("Muestras a promediar durante la calibración")]
    [Range(10, 60)]
    public int calibrationSamples = 30;

    // ── Bloqueo Táctil Dual ───────────────────────────────────────────────────
    [Header("Bloqueo Táctil Dual")]
    [Tooltip("Si está activo, se deben presionar ambos botones laterales para mover")]
    public bool requireBothButtonsToMove = true;

    [Tooltip("Ancho de cada botón lateral (píxeles)")]
    public float buttonWidth = 180f;

    [Tooltip("Alto de cada botón lateral (píxeles)")]
    public float buttonHeight = 180f;

    [Tooltip("Margen desde los bordes laterales (píxeles)")]
    public float buttonMargin = 30f;

    [Tooltip("Color del botón izquierdo")]
    public Color leftButtonColor = new Color(0.2f, 0.6f, 1f, 0.7f);

    [Tooltip("Color del botón derecho")]
    public Color rightButtonColor = new Color(0.2f, 0.6f, 1f, 0.7f);

    [Tooltip("Tecla para simular botón izquierdo en PC/Editor")]
    public KeyCode desktopLeftKey = KeyCode.Q;

    [Tooltip("Tecla para simular botón derecho en PC/Editor")]
    public KeyCode desktopRightKey = KeyCode.P;

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

    // Tilt suavizado como vector 2D (x=side, y=forward) en grados
    private Vector2 smoothedTilt = Vector2.zero;

    // Estado de los botones
    private bool leftButtonPressed = false;
    private bool rightButtonPressed = false;
    private GameObject buttonCanvas;

    // ────────────────────────────────────────────────────────────────────────
    void Start()
    {
        initialRotation = transform.rotation;
        targetRotation = initialRotation;

        if (requireBothButtonsToMove && IsMobilePlatform())
            CreateDualButtons();

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
        smoothedTilt = Vector2.zero;

        if (calibrationDelay > 0f)
            yield return new WaitForSeconds(calibrationDelay);

        // ── Warm-up: descarta el primer 30% de muestras ───────────────────
        // Los giroscopios suelen necesitar algunos frames para estabilizarse.
        int warmUpSamples = Mathf.Max(1, calibrationSamples / 3);
        for (int i = 0; i < warmUpSamples; i++)
            yield return new WaitForSeconds(0.016f);

        // ── Promedio con peso uniforme usando Slerp acumulativo ───────────
        // Se acumula con peso 1/N en cada paso → resultado idéntico a
        // la media aritmética en espacio de quaterniones, sin sesgo inicial.
        int mainSamples = calibrationSamples - warmUpSamples;
        Quaternion avg = GyroToUnity(gyro.attitude);

        for (int i = 1; i < mainSamples; i++)
        {
            yield return new WaitForSeconds(0.016f);
            // Peso uniforme: la muestra i tiene peso 1/(i+1) relativo al acumulado.
            // El resultado converge al promedio simple de todas las muestras.
            float w = 1f / (i + 1f);
            avg = Quaternion.Slerp(avg, GyroToUnity(gyro.attitude), w);
        }

        calibrationOffset = Quaternion.Inverse(avg);
        calibrated = true;
        Debug.Log("[GyroController] Calibrado.");
    }

    // ────────────────────────────────────────────────────────────────────────
    void Update()
    {
        // Actualizar estado de botones en PC/Editor
        if (!IsMobilePlatform() && requireBothButtonsToMove)
        {
            leftButtonPressed = Input.GetKey(desktopLeftKey);
            rightButtonPressed = Input.GetKey(desktopRightKey);
        }

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

        // Verificar si los botones están presionados (solo si se requiere)
        if (requireBothButtonsToMove && !BothButtonsPressed())
        {
            ResetTilt();
            return;
        }

        // ── 1. Actitud calibrada ──────────────────────────────────────────
        Quaternion calibratedAttitude = calibrationOffset * GyroToUnity(gyro.attitude);

        // ── 2. Vector mundo-arriba proyectado en espacio sensor ───────────
        Vector3 worldUpInSensor = Quaternion.Inverse(calibratedAttitude) * Vector3.up;

        // ── 3. Asignación de ejes según orientación landscape ─────────────
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

        if (landscapeFlipSide)
            rawSide = -rawSide;

        // ── 4. Sensibilidad + conversión a grados (Asin estabilizado) ─────
        rawForward *= sensitivityForward;
        rawSide *= sensitivitySide;

        float tiltForward = Mathf.Asin(Mathf.Clamp(rawForward, -0.99f, 0.99f)) * Mathf.Rad2Deg;
        float tiltSide = Mathf.Asin(Mathf.Clamp(rawSide, -0.99f, 0.99f)) * Mathf.Rad2Deg;

        // ── 5. Remapeo radial + dead zone suave + curva de respuesta ──────
        //   Trabajar en coordenadas polares permite:
        //     a) Dead zone uniforme en todas las direcciones (incluidas esquinas).
        //     b) Curva de respuesta aplicada a la magnitud total, no por eje.
        //     c) Las esquinas diagonales alcanzan la misma magnitud máxima
        //        que los ejes puros.
        Vector2 tilt = new Vector2(tiltSide, tiltForward);
        float magnitude = tilt.magnitude;

        float remappedMag = 0f;

        if (magnitude > deadZoneInner)
        {
            float outer = Mathf.Max(deadZoneOuter, deadZoneInner + 0.001f);

            if (magnitude >= outer)
            {
                // Fuera de la zona de transición: mapeo directo
                // Escala para que outer corresponda a 0° y maxTilt a maxTiltAngle
                float range = maxTiltAngle - outer;
                remappedMag = outer + Mathf.Clamp(magnitude - outer, 0f, range);
            }
            else
            {
                // Zona de transición suave (smoothstep entre inner y outer)
                float t = (magnitude - deadZoneInner) / (outer - deadZoneInner);
                t = t * t * (3f - 2f * t);  // smoothstep
                remappedMag = Mathf.Lerp(0f, outer, t);
            }

            // Curva de respuesta sobre la magnitud normalizada [0,1]
            float normalizedMag = Mathf.Clamp01(remappedMag / maxTiltAngle);
            float curvedMag = Mathf.Pow(normalizedMag, responseCurve) * maxTiltAngle;

            // Re-proyectar manteniendo la dirección original
            tilt = tilt.normalized * curvedMag;
        }
        else
        {
            tilt = Vector2.zero;
        }

        // ── 6. Suavizado 2D unificado ─────────────────────────────────────
        if (tiltSmoothing > 0f)
        {
            float smoothT = 1f - Mathf.Exp(-tiltSmoothing * Time.deltaTime);
            smoothedTilt = Vector2.Lerp(smoothedTilt, tilt, smoothT);
        }
        else
        {
            smoothedTilt = tilt;
        }

        // ── 7. Rotación objetivo ──────────────────────────────────────────
        // smoothedTilt.x = side, smoothedTilt.y = forward
        targetRotation = initialRotation * Quaternion.Euler(smoothedTilt.y, 0f, smoothedTilt.x);

        // ── 8. Debug ──────────────────────────────────────────────────────
        if (smoothedTilt.magnitude > 0.5f && Time.frameCount % 120 == 0)
        {
            Debug.Log(string.Format(
                "[GyroController] Forward:{0:F1}° Side:{1:F1}° | Magnitud:{2:F1}°",
                smoothedTilt.y, smoothedTilt.x, smoothedTilt.magnitude));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    void ResetTilt()
    {
        float smoothT = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        smoothedTilt = Vector2.Lerp(smoothedTilt, Vector2.zero, smoothT);
        targetRotation = initialRotation * Quaternion.Euler(smoothedTilt.y, 0f, smoothedTilt.x);
    }

    void UpdateWithMouse()
    {
        Vector3 mousePos = Input.mousePosition;
        float normalizedX = mousePos.x / Screen.width;
        float normalizedY = mousePos.y / Screen.height;

        float targetX = (normalizedX * 2f - 1f) * maxTiltAngle;
        float targetZ = (normalizedY * 2f - 1f) * maxTiltAngle;

        // Usar el mismo smoothedTilt que el giroscopio
        Vector2 targetTilt = new Vector2(-targetX, targetZ);
        float smoothT = 1f - Mathf.Exp(-tiltSmoothing * Time.deltaTime);
        smoothedTilt = Vector2.Lerp(smoothedTilt, targetTilt, smoothT);

        targetRotation = initialRotation * Quaternion.Euler(smoothedTilt.y, 0f, smoothedTilt.x);
    }

    // ────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Convierte la actitud del giroscopio al sistema de coordenadas de Unity.
    /// RHS → LHS: se niegan los ejes X e Y del quaternion.
    /// </summary>
    Quaternion GyroToUnity(Quaternion q)
    {
        return new Quaternion(-q.x, -q.y, q.z, q.w);
    }

    // ── Funciones para los botones duales ───────────────────────────────────
    bool IsMobilePlatform()
    {
        return Application.platform == RuntimePlatform.Android ||
               Application.platform == RuntimePlatform.IPhonePlayer;
    }

    bool BothButtonsPressed()
    {
        bool pressed = leftButtonPressed && rightButtonPressed;

        return pressed;
    }

    void CreateDualButtons()
    {
        // Asegurar que exista EventSystem
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
            Debug.Log("[GyroController] EventSystem creado.");
        }

        // Crear Canvas
        buttonCanvas = new GameObject("DualButtonCanvas");
        Canvas canvas = buttonCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        buttonCanvas.AddComponent<CanvasScaler>();
        buttonCanvas.AddComponent<GraphicRaycaster>();

        Debug.Log("[GyroController] Canvas creado.");

        // Crear botón izquierdo
        CreateButton("LeftGateButton", leftButtonColor,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(buttonMargin, 0), true);

        // Crear botón derecho
        CreateButton("RightGateButton", rightButtonColor,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-buttonMargin, 0), false);

        Debug.Log("[GyroController] Botones duales creados correctamente.");
    }

    void CreateButton(string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, bool isLeft)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(buttonCanvas.transform, false);

        // Image
        Image image = buttonObj.AddComponent<Image>();
        image.color = color;

        // Button (para feedback visual)
        Button button = buttonObj.AddComponent<Button>();

        // Configurar colores del botón para feedback al presionar
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = new Color(color.r, color.g, color.b, color.a + 0.2f);
        colors.pressedColor = new Color(color.r, color.g, color.b, color.a + 0.3f);
        colors.disabledColor = color;
        button.colors = colors;

        // RectTransform
        RectTransform rect = buttonObj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMax.x, 0.5f);
        rect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
        rect.anchoredPosition = anchoredPosition;

        // Detector táctil
        ButtonTouchDetector detector = buttonObj.AddComponent<ButtonTouchDetector>();
        detector.Setup(this, isLeft);

        // Texto del botón
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);

        Text text = textObj.AddComponent<Text>();
        text.text = isLeft ? "←\nMANTEN" : "→\nMANTEN";
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 36;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;

        // Sombra del texto para mejor visibilidad
        Shadow shadow = textObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.5f);
        shadow.effectDistance = new Vector2(2, -2);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 10);
        textRect.offsetMax = new Vector2(-10, -10);
        
    }

    // Métodos públicos para ser llamados desde ButtonTouchDetector
    public void SetLeftButtonPressed(bool pressed)
    {
        leftButtonPressed = pressed;
        if (pressed)
            Debug.Log("[GyroController] Botón IZQUIERDO presionado");
        else
            Debug.Log("[GyroController] Botón IZQUIERDO liberado");
    }

    public void SetRightButtonPressed(bool pressed)
    {
        rightButtonPressed = pressed;
        if (pressed)
            Debug.Log("[GyroController] Botón DERECHO presionado");
        else
            Debug.Log("[GyroController] Botón DERECHO liberado");
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

// ────────────────────────────────────────────────────────────────────────────
/// <summary>
/// Script auxiliar para detectar presión en botones (compatible con Unity 2017)
/// </summary>
public class ButtonTouchDetector : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private GyroscopeSceneController controller;
    private bool isLeftButton;

    public void Setup(GyroscopeSceneController ctrl, bool left)
    {
        controller = ctrl;
        isLeftButton = left;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (controller == null) return;

        if (isLeftButton)
            controller.SetLeftButtonPressed(true);
        else
            controller.SetRightButtonPressed(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (controller == null) return;

        if (isLeftButton)
            controller.SetLeftButtonPressed(false);
        else
            controller.SetRightButtonPressed(false);
    }
}