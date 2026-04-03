using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Debug UI simple para mostrar valores del giroscopio
/// </summary>
public class GyroDebugDisplay : MonoBehaviour
{
    [Header("Textos UI")]
    public Text txtForward;     // Ángulo adelante/atrás
    public Text txtSide;        // Ángulo lateral
    public Text txtRawAccel;    // Acelerómetro crudo
    
    [Header("Referencia")]
    public GyroscopeSceneController controller; // Arrastra el objeto con tu script
    
    void Update()
    {
        if (controller == null) return;
        
        // Usando reflexión para acceder a valores privados de tu script
        var controllerType = controller.GetType();
        
        // Obtener ángulos (asumiendo que targetRotation contiene la rotación)
        Vector3 angles = controller.transform.rotation.eulerAngles;
        
        // Mostrar en UI
        if (txtForward != null)
            txtForward.text = string.Format("Adelante/atrás: {0:F1}°", NormalizeAngle(angles.x));
        
        if (txtSide != null)
            txtSide.text = string.Format("Lateral: {0:F1}°", NormalizeAngle(angles.z));
        
        if (txtRawAccel != null)
        {
            Vector3 accel = Input.acceleration;
            txtRawAccel.text = string.Format("Acel: ({0:F2}, {1:F2}, {2:F2})", 
                accel.x, accel.y, accel.z);
        }
    }
    
    float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}
