/*using UnityEngine;

public class MouseCameraControl : MonoBehaviour
{
    public float sensitivity = 2f; // 滑鼠靈敏度
    private float rotationX = 0f;

    void Update()
    {
        // 獲取滑鼠輸入
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        // 垂直旋轉限制
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, -90f, 90f);

        // 更新攝影機旋轉
        transform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
        transform.parent.Rotate(Vector3.up * mouseX);
    }
}
*/
using UnityEngine;

public class FreeCameraController : MonoBehaviour
{
    public float moveSpeed = 5f; // 移動速度
    public float boostMultiplier = 2f; // 加速移動倍數（按住 Shift 時）
    public float sensitivity = 100f; // 滑鼠靈敏度

    private float rotationX = 0f; // 攝影機的垂直旋轉角度
    private float rotationY = 0f; // 攝影機的水平旋轉角度

    void Start()
    {
        // 鎖定滑鼠
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 初始化旋轉角度
        Vector3 rotation = transform.eulerAngles;
        rotationX = rotation.x;
        rotationY = rotation.y;
    }

    void Update()
    {
        // 滑鼠控制攝影機旋轉
        //float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.deltaTime;
        //float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.deltaTime;
        float mouseX = Input.GetAxis("Mouse X") * sensitivity; 
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        rotationY += mouseX;
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, -90f, 90f); // 限制垂直旋轉角度

        transform.rotation = Quaternion.Euler(rotationX, rotationY, 0f);

        // 鍵盤控制攝影機移動
        float horizontal = Input.GetAxis("Horizontal"); // A/D 或 ←/→
        float vertical = Input.GetAxis("Vertical"); // W/S 或 ↑/↓
        float upDown = 0f;

        if (Input.GetKey(KeyCode.E)) upDown = 1f; // E 上升
        if (Input.GetKey(KeyCode.Q)) upDown = -1f; // Q 下降

        Vector3 direction = transform.forward * vertical + transform.right * horizontal + transform.up * upDown;
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? boostMultiplier : 1f); // 按住 Shift 加速

        transform.position += direction * speed * Time.deltaTime;

        // 按 Escape 釋放滑鼠
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
