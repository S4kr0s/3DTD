using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private GameObject cameraAnchor;
    [SerializeField] private float zoomSpeed;
    [SerializeField] private float moveSpeed;
    [SerializeField] private float rotateSpeed;
    [SerializeField] private float currentVerticality = 0;
    private Vector3 zoomLocation;

    private void Start()
    {
        zoomLocation = this.transform.position;
    }

    private void Update()
    {
        cameraAnchor.transform.rotation = Quaternion.Euler(cameraAnchor.transform.eulerAngles.x, this.gameObject.transform.localEulerAngles.y, cameraAnchor.transform.eulerAngles.z);

        ZoomAround();
        MoveAround();

        //float speed = 0.1f;
        //this.transform.position = Vector3.Lerp(transform.position, zoomLocation, speed);

        if (Input.GetKey(KeyCode.Mouse1))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            RotateAround();
        }

        if (!Input.GetKey(KeyCode.Mouse1))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void ZoomAround()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        //if (scroll != 0)
        //{
        //    zoomLocation += (transform.forward * (scroll * zoomSpeed * Time.deltaTime));
        //}
        
        this.transform.position += transform.forward * scroll * zoomSpeed * Time.unscaledDeltaTime;
    }

    private void MoveAround()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        float height = (Input.GetKey(KeyCode.Q) ? -1 : 0) + (Input.GetKey(KeyCode.E) ? 1 : 0);

        cameraAnchor.transform.position += Quaternion.Euler(new Vector3(0, transform.eulerAngles.y, 0)) * new Vector3(horizontal, height/2, vertical) * moveSpeed * Time.unscaledDeltaTime;
    }

    private void RotateAround()
    {
        float horizontal = Input.GetAxis("Mouse X");
        float vertical = Input.GetAxis("Mouse Y");

        this.gameObject.transform.RotateAround(cameraAnchor.transform.position, Vector3.up, rotateSpeed * horizontal);

        currentVerticality = Mathf.Clamp(currentVerticality + vertical, -9, 26);
        if (currentVerticality >= 25 || currentVerticality <= -8)
            return;

        this.gameObject.transform.RotateAround(cameraAnchor.transform.position, transform.TransformDirection(Vector3.right), rotateSpeed * -vertical);
    }
}
