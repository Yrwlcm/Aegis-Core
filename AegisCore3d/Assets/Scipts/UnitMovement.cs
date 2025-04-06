using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class UnitMovement : MonoBehaviour
{
    private Camera mainCamera;
    private NavMeshAgent navAgent;
    public LayerMask ground;

    private void Start()
    {
        mainCamera = Camera.main;
        navAgent = GetComponent<NavMeshAgent>();
    }
    
    private void Update()
    {
        if (!Input.GetMouseButtonDown((int)MouseButton.Right)) return;
        
        var ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out var hit, Mathf.Infinity, ground))
        {
            navAgent.SetDestination(hit.point);
        }
    }
}
