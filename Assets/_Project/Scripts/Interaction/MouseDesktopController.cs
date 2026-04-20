using UnityEngine;

public class MouseDesktopController : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float lookSensitivity = 3f;
    public float minFov = 60f;
    public float maxFov = 110f;
    public RectTransform LabHUD;

    private Camera mainCamera;
    private ChargeSpawnManager spawnManager;
    private float yaw;
    private float pitch;

    private void Start()
    {
        mainCamera = Camera.main;
        spawnManager = FindObjectOfType<ChargeSpawnManager>();
        if (mainCamera != null)
        {
            Vector3 e = mainCamera.transform.eulerAngles;
            yaw = e.y;
            pitch = e.x;
        }
    }

    private void Update()
    {
#if UNITY_EDITOR || !UNITY_ANDROID
        if (mainCamera == null) return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 move = new Vector3(h, 0f, v) * (moveSpeed * Time.deltaTime);
        transform.position += transform.TransformDirection(move);

        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * lookSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
            pitch = Mathf.Clamp(pitch, -80f, 80f);
            mainCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            mainCamera.fieldOfView = Mathf.Clamp(mainCamera.fieldOfView - scroll * 20f, minFov, maxFov);
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit) && spawnManager != null)
            {
                spawnManager.SpawnCharge(hit.point, ChargeType.Positive);
            }
        }

        if (Input.GetKeyDown(KeyCode.N))
        {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit) && spawnManager != null)
            {
                spawnManager.SpawnCharge(hit.point, ChargeType.Negative);
            }
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit) && spawnManager != null)
            {
                spawnManager.ClearAll();
            }
        }
        
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if(LabHUD.gameObject.activeSelf)
            {
                LabHUD.gameObject.SetActive(false);
            }
            else
            {
                LabHUD.gameObject.SetActive(true);
            }
        }
#endif
    }
}
