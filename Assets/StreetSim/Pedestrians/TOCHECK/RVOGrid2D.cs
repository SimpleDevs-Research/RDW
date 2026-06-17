using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
    using UnityEditor;
#endif
using RVO;
using Unity.Mathematics;

public class RVOGrid2D : MonoBehaviour
{
    [Header("=== REFERENCES ===")]
    public Transform lowerRef;
    public Transform upperRef;

    [Header("=== CONTROLS ===")]
    public float gridCellSize = 1f;
    
    [Header("=== GRID DATA ===")]
    [SerializeField] private Vector3 _center = Vector3.zero;
    public Vector3 center => _center;
    [SerializeField] private Vector2 _dimensions = Vector2.zero;
    public Vector2 dimensions => _dimensions;
    [SerializeField] private int _numCells = 0;
    public int numCells => _numCells;
    [SerializeField] private Vector2Int _numCellsPerAxis = Vector2Int.zero;
    public Vector2Int numCellsPerAxis => _numCellsPerAxis;
    private Vector3 upwardDir = Vector3.up;
    [SerializeField] private RVO.Grid.Grid2DCell[] _cells;
    public RVO.Grid.Grid2DCell[] cells => _cells;

    [Header("=== DEBUG ===")]
    public bool gizmos_center = true;
    public bool gizmos_orthogonal = true;
    public bool gizmos_cells = true;
    private bool gizmos => gizmos_center || gizmos_orthogonal || gizmos_cells;

    #if UNITY_EDITOR
    void OnDrawGizmos() {
        if (!gizmos) return;
        if (gizmos_center) {
            // Draw the center of the grid
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(_center, 0.25f);
        }
        if (gizmos_orthogonal) {
            // Draw orthogonal norms of 2D flat grid
            Handles.DrawBezier(center-upwardDir*5f, center+upwardDir*5f, center-upwardDir*5f, center+upwardDir*5f, Color.yellow, null, 3);
        }
        if (gizmos_cells) {
            // Draw inidividual cells
            for(int x = 0; x < _numCellsPerAxis.x; x++) {
                for(int y = 0; y < _numCellsPerAxis.y; y++) {
                    Vector3 v1 = lowerRef.position + new Vector3(gridCellSize*x, 0.25f, gridCellSize*y);
                    Vector3 v2 = v1 + new Vector3(gridCellSize, 0f, 0f);
                    Vector3 v3 = v2 + new Vector3(0f, 0f, gridCellSize);
                    Vector3 v4 = v3 + new Vector3(-gridCellSize, 0f, 0f);
                    Vector3[] verts = new Vector3[]{v1,v2,v3,v4};
                    int projectedIndex = x*_numCellsPerAxis.y + y;
                    Handles.DrawSolidRectangleWithOutline(
                        verts, 
                        //new Color(1f, 0f, 0f, Mathf.Clamp((float)cells[projectedIndex].numAgents/3f,0f,1f)), 
                        new Color(1f, 0f, 0f, 0.1f), 
                        new Color(0, 0, 0, 1)
                    );
                }
            }
        }
    }
    #endif

    /*
    public void SetController(RobotManager manager) {
        this.manager = manager;
        manuallyUpdate = true;
    }

    private void Start() {
        ManualUpdate();
    }

    // Update is called once per frame
    private void Update()
    {
        if (manuallyUpdate) return;
        ManualUpdate();
    }

    public void ManualUpdate() {
        if (_cells.Length == 0) return;
        // Clear the grid
        foreach(RVO.Grid.Grid2DCell cell in _cells) cell.ClearCell();
        
        // Update grid counts
        if (manager == null) return;
        foreach(RVO_Robot robot in manager.robots) {
            int cellIndex = RVO.Grid.GetProjectedIndex(robot.position3D, _center, _numCellsPerAxis, gridCellSize);
            _cells[cellIndex].AddAgent(robot);
            robot.gridCellIndex = cellIndex;
        }
    }
    */

    public void SetLowerRef(Transform t) {
        lowerRef = t;
    }
    public void SetUpperRef(Transform t) {
        upperRef = t;
    }
    public void SetRefs(Transform lower, Transform upper) {
        SetLowerRef(lower);
        SetUpperRef(upper);
    }
    public void GenerateGrid() {
        if (lowerRef == null || upperRef == null) {
            Debug.LogError("Cannot generate a grid if the upper and lower reference transforms are not set");
            return;
        }
        Vector3 diff = upperRef.position - lowerRef.position;
        _dimensions = new Vector2(Mathf.Abs(diff.x), Mathf.Abs(diff.z));
        _center = (lowerRef.position + upperRef.position)/2f;
        _numCellsPerAxis = new Vector2Int(
            Mathf.FloorToInt(_dimensions.x / gridCellSize),
            Mathf.FloorToInt(_dimensions.y / gridCellSize)
        );
        _numCells = _numCellsPerAxis.x * _numCellsPerAxis.y;
        _cells = new RVO.Grid.Grid2DCell[_numCells];
        for(int x = 0; x < _numCellsPerAxis.x; x++) {
            for(int y = 0; y < _numCellsPerAxis.y; y++) {
                int projectedIndex = RVO.Grid.GetProjectedIndex(x,y,_numCellsPerAxis);
                _cells[projectedIndex] = new RVO.Grid.Grid2DCell(this, x, y, projectedIndex);
            }
        }
    }
    public void ClearGrid() {
        foreach(RVO.Grid.Grid2DCell cell in _cells) cell.ClearCell();
    }
    public Vector2Int GetGridXY(Vector3 position) {
        return RVO.Grid.GetGridXY(position, _center, _numCellsPerAxis, gridCellSize);
    }
    public int GetProjectedIndex(Vector2Int xy) {
        return RVO.Grid.GetProjectedIndex(xy, _numCellsPerAxis);
    }
    public int GetProjectedIndex(Vector3 position) {
        return RVO.Grid.GetProjectedIndex(position, _center, _numCellsPerAxis, gridCellSize);
    }
    public void UpdateCell(int projectedIndex, RVO_Robot robot) {
        _cells[projectedIndex].AddAgent(robot);
    }
    public int UpdateCell(RVO_Robot robot) {
        int projectedIndex = GetProjectedIndex(robot.position3D);
        UpdateCell(projectedIndex, robot);
        return projectedIndex;
    }
    public void ResetGrid() {
        _center = Vector3.zero;
        _dimensions = Vector2Int.zero;
        _numCells = 0;
        _numCellsPerAxis = Vector2Int.zero;
        _cells = new RVO.Grid.Grid2DCell[0];
    }
}
