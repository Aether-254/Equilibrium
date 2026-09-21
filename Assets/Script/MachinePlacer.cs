using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class MachinePlacer : MonoBehaviour
{
    public GameObject machinePrefab;
    public GameObject conveyorPrefab;      // 新增
    public float gridSize = 1f;

    private bool placingConveyor = false;  // 新增：当前模式

    private HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
    private Dictionary<Vector2Int, GameObject> machines = new Dictionary<Vector2Int, GameObject>();

    void Update()
    {
        // Tab 切换模式
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            placingConveyor = !placingConveyor;
            Debug.Log(placingConveyor ? "当前：放传送带" : "当前：放机器");
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
            TryPlace();
        else if (Mouse.current.rightButton.wasPressedThisFrame)
            TryRemove();
    }

    Vector2Int GetMouseCell()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mousePos.z = 0;
        int cellX = Mathf.FloorToInt(mousePos.x / gridSize);
        int cellY = Mathf.FloorToInt(mousePos.y / gridSize);
        return new Vector2Int(cellX, cellY);
    }

    void TryPlace()
    {
        Vector2Int cell = GetMouseCell();
        if (occupiedCells.Contains(cell))
        {
            Debug.Log("这个格子已经被占了！");
            return;
        }

        Vector3 spawnPos = new Vector3(
            cell.x * gridSize + gridSize / 2f,
            cell.y * gridSize + gridSize / 2f,
            0
        );

        GameObject prefab = placingConveyor ? conveyorPrefab : machinePrefab;
        if (prefab == null) { Debug.LogError("Prefab 没设置！"); return; }

        GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity);
        occupiedCells.Add(cell);
        machines[cell] = obj;
    }

    void TryRemove()
    {
        Vector2Int cell = GetMouseCell();
        if (!occupiedCells.Contains(cell)) { Debug.Log("空的！"); return; }

        Destroy(machines[cell]);
        machines.Remove(cell);
        occupiedCells.Remove(cell);
    }
}