using UnityEngine;

public class CameraControl : MonoBehaviour
{
    [SerializeField, Range(0.001f, 2.0f)]
    float scrollSpeed = 0.5f;

    [SerializeField, Range(0.001f, 2.0f)]
    float dragSpeed = 0.1f;

    [SerializeField, Range(0.001f, 2.0f)]
    float keySpeed = 0.5f;

    private Vector3 _pos;
    private Vector3 _dir;
    private Vector3 _right = Vector3.right;
    private float _yaw = 90.0f;
    private float _pitch = 0.0f;

    private bool _mouseDown = false;
    private Vector3 _mousePos = Vector3.zero;

    private void OnEnable()
    {
        _pos = transform.position;
        _dir = Vector3.forward;
        UpdateDir();
        UpdateCamera();
    }

    private void Update()
    {
        bool updated = false;
        // check for scroll
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if(scroll > 0.0f)
        {
            _pos += _dir * scrollSpeed;
            updated = true;
        }
        else if(scroll < 0.0f)
        {
            _pos -= _dir * scrollSpeed;
            updated = true;
        }
        if(Input.GetMouseButtonDown(0))
        {
            _mouseDown = true;
            _mousePos = Input.mousePosition;
        }
        // check for click and drag
        if (Input.GetMouseButtonUp(0))
        {
            _mouseDown = false;
        }
        // CTRL+Space for camera render mode toggle
        if(Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Space))
        {
            ToggleComputeControl();
        }
        if(_mouseDown)
        {
            Vector3 mousePos = Input.mousePosition;
            Vector3 delta = mousePos - _mousePos;
            _mousePos = mousePos;
            _yaw += delta.x * dragSpeed;
            _pitch += -delta.y * dragSpeed;
            _pitch = Mathf.Clamp(_pitch, -89.9f, 89.9f);
            UpdateDir();
            updated = true;
        }
        // check for key controls
        if(Input.GetKey(KeyCode.A))
        {
            _pos += _right * keySpeed;
            updated = true;
        }
        if(Input.GetKey(KeyCode.D))
        {
            _pos -= _right * keySpeed;
            updated = true;
        }
        if(Input.GetKey(KeyCode.W))
        {
            _pos += _dir * keySpeed;
            updated = true;
        }
        if(Input.GetKey(KeyCode.S))
        {
            _pos -= _dir * keySpeed;
            updated = true;
        }
        if (updated) UpdateCamera();
    }

    private void UpdateDir()
    {
        _dir.x = Mathf.Cos(Mathf.Deg2Rad * _yaw) * Mathf.Cos(Mathf.Deg2Rad * _pitch);
        _dir.y = Mathf.Sin(Mathf.Deg2Rad * _pitch);
        _dir.z = Mathf.Sin(Mathf.Deg2Rad * _yaw) * Mathf.Cos(Mathf.Deg2Rad * _pitch);
        _dir = Vector3.Normalize(_dir);
        _right = Vector3.Normalize(Vector3.Cross(_dir, Vector3.up));
    }

    private void UpdateCamera()
    {
        transform.position = _pos;
        transform.LookAt(_pos + _dir);
    }

    private void ToggleComputeControl()
    {
        Tracing.ComputeLock = !Tracing.ComputeLock;
        Tracing.ComputeLockUpdated = true;
        Camera currentCamera = GetComponent<Camera>();
        if(Tracing.ComputeLock)
        {
            currentCamera.clearFlags = CameraClearFlags.Skybox;
            currentCamera.cullingMask = 1 << LayerMask.NameToLayer("Default");
        }
        else
        {
            currentCamera.clearFlags = CameraClearFlags.Nothing;
            currentCamera.cullingMask = 0;
        }
    }
}
