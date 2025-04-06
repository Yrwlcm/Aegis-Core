using System;
using Cinemachine;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

public class CameraSystem : MonoBehaviour
{
    [SerializeField] private int moveSpeed = 35;
    [SerializeField] private int rotateSpeed = 100;
    [SerializeField] private int edgeScrollingBorder = 20;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private CinemachineVirtualCamera cinemachineVirtualCamera;
    [SerializeField] private float followOffsetMin = 50f;
    [SerializeField] private float followOffsetMax = 100f;
    [SerializeField] private float zoomTransitionSpeed = 10f;
    [SerializeField] private float zoomAmount = 3f;

    private bool dragPanMoveActive;
    private Vector2 lastMousePosition;
    private Vector3 dragOrigin;
    private Vector3 followOffset;

    private void Awake()
    {
        followOffset = cinemachineVirtualCamera.GetCinemachineComponent<CinemachineTransposer>().m_FollowOffset;
    }

    void Update()
    {
        var inputDirection = new Vector3();

        HandleCameraZoom();

        inputDirection = HandleCameraPan();

        if (!dragPanMoveActive)
        {
            if (!CheckEdgeScrollDirection(out inputDirection))
                CheckInputDirection(out inputDirection);

            var moveDirection = transform.forward * inputDirection.z + transform.right * inputDirection.x;

            inputDirection = moveDirection.normalized * moveSpeed * Time.deltaTime;
        }

        transform.position += inputDirection;

        CheckRotateDirection(out var rotateDirection);

        transform.eulerAngles += new Vector3(0, rotateDirection * rotateSpeed * Time.deltaTime, 0);
    }

    private bool CheckRotateDirection(out int rotateDirection)
    {
        var localRotation = 0;

        if (Input.GetKey(KeyCode.Q)) localRotation = 1;
        if (Input.GetKey(KeyCode.E)) localRotation = -1;

        rotateDirection = localRotation;
        return localRotation != 0;
    }

    private Vector3 HandleCameraPan()
    {
        if (Input.GetMouseButtonDown((int)MouseButton.Middle) && !dragPanMoveActive)
        {
            dragOrigin = ScreenPositionToGroundRaycast(Input.mousePosition);
            dragPanMoveActive = true;
        }

        if (!Input.GetMouseButton((int)MouseButton.Middle))
        {
            dragPanMoveActive = false;
            return new Vector3();
        }

        var difference = dragOrigin - ScreenPositionToGroundRaycast(Input.mousePosition);
        difference.y = 0;
        return difference;
    }

    private void HandleCameraZoom()
    {
        var zoomDirection = followOffset.normalized;
        if (Input.mouseScrollDelta.y > 0)
        {
            followOffset -= zoomDirection * zoomAmount;
        }
        else if (Input.mouseScrollDelta.y < 0)
        {
            followOffset += zoomDirection * zoomAmount;
        }

        if (followOffset.magnitude < followOffsetMin)
        {
            followOffset = zoomDirection * followOffsetMin;
        }
        else if (followOffset.magnitude > followOffsetMax)
        {
            followOffset = zoomDirection * followOffsetMax;
        }

        var cinemachineOffset = cinemachineVirtualCamera.GetCinemachineComponent<CinemachineTransposer>();

        cinemachineOffset.m_FollowOffset =
            Vector3.Lerp(cinemachineOffset.m_FollowOffset, followOffset, zoomTransitionSpeed * Time.deltaTime);
    }

    private bool CheckInputDirection(out Vector3 moveDirection)
    {
        var localDirection = new Vector3();

        if (Input.GetKey(KeyCode.UpArrow)) localDirection.z = 1;
        if (Input.GetKey(KeyCode.DownArrow)) localDirection.z = -1;
        if (Input.GetKey(KeyCode.LeftArrow)) localDirection.x = -1;
        if (Input.GetKey(KeyCode.RightArrow)) localDirection.x = 1;

        moveDirection = localDirection;
        return localDirection.magnitude > 0;
    }

    private bool CheckEdgeScrollDirection(out Vector3 moveDirection)
    {
        var localDirection = new Vector3();
        if (Input.mousePosition.x < edgeScrollingBorder) localDirection.x = -1;
        if (Input.mousePosition.y < edgeScrollingBorder) localDirection.z = -1;
        if (Input.mousePosition.x > Screen.width - edgeScrollingBorder) localDirection.x = +1;
        if (Input.mousePosition.y > Screen.height - edgeScrollingBorder) localDirection.z = +1;

        moveDirection = localDirection;
        return localDirection.magnitude > 0;
    }


    //ScreenToWorldPoint у mainCamera не работает, почему - не знаю
    //Загуглить не получилось, при этом все остальные функции которые конвертируют ворлд в скрин и наоборот работают
    //Поэтому пришлось писать этот метод
    private Vector3 ScreenPositionToGroundRaycast(Vector3 screenPosition)
    {
        Physics.Raycast(mainCamera.ScreenPointToRay(screenPosition), out var hit, 100_000, LayerMask.GetMask("Ground"));

        return hit.point;
    }
}