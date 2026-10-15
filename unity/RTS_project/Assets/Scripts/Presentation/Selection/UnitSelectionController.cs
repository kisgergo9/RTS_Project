using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class UnitSelectionController : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField, Min(1f)] private float dragThreshold = 8f;

    private readonly HashSet<UnitSelection> _selectedUnits = new();

    private Vector2 _dragStart;
    private Vector2 _currentMousePosition;
    private bool _isDragging;

    private void Awake()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current == null || worldCamera == null)
            return;

        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            RotateSelectedUnitsTowardsMouse();
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            _dragStart = mousePosition;
            _currentMousePosition = mousePosition;
            _isDragging = false;
        }

        if (Mouse.current.leftButton.isPressed)
        {
            _currentMousePosition = mousePosition;

            if (Vector2.Distance(_dragStart, mousePosition)
                >= dragThreshold)
            {
                _isDragging = true;
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (_isDragging)
                SelectUnitsInRectangle();
            else
                SelectUnitAtMousePosition();

            _isDragging = false;
        }
    }

    private void RotateSelectedUnitsTowardsMouse()
    {
        if (_selectedUnits.Count == 0)
            return;

        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        Ray ray = worldCamera.ScreenPointToRay(mousePosition);

        // A játékvilág XY síkjának z koordinátája 0.
        Plane worldPlane = new Plane(Vector3.forward, Vector3.zero);

        if (!worldPlane.Raycast(ray, out float distance))
            return;

        Vector3 mouseWorldPosition = ray.GetPoint(distance);

        foreach (UnitSelection unit in _selectedUnits)
        {
            if (unit == null)
                continue;

            Vector2 direction =
                (Vector2)(mouseWorldPosition - unit.transform.position);

            // Ne számoljunk irányt, ha a kattintás az egység középpontjára esik.
            if (direction.sqrMagnitude < 0.0001f)
                continue;

            float angle = Mathf.Atan2(
                direction.x,
                direction.y) * Mathf.Rad2Deg;

            angle = Mathf.Repeat(angle, 360f);

            DirectionalSpriteRenderer directionalRenderer =
                unit.GetComponent<DirectionalSpriteRenderer>();

            if (directionalRenderer != null)
                directionalRenderer.SetAngle(angle);
        }
    }

    private void SelectUnitAtMousePosition()
    {
        ClearSelection();

        Ray ray = worldCamera.ScreenPointToRay(_currentMousePosition);
        RaycastHit2D hit = Physics2D.GetRayIntersection(ray);

        if (hit.collider == null)
            return;

        UnitSelection unit =
            hit.collider.GetComponentInParent<UnitSelection>();

        if (unit != null)
            AddToSelection(unit);
    }

    private void SelectUnitsInRectangle()
    {
        ClearSelection();

        Vector2 min = Vector2.Min(_dragStart, _currentMousePosition);
        Vector2 max = Vector2.Max(_dragStart, _currentMousePosition);

        UnitSelection[] allUnits =
            FindObjectsByType<UnitSelection>(
                FindObjectsSortMode.None);

        foreach (UnitSelection unit in allUnits)
        {
            if (unit == null || !unit.isActiveAndEnabled)
                continue;

            Vector3 screenPosition =
                worldCamera.WorldToScreenPoint(unit.transform.position);

            if (screenPosition.z < 0f)
                continue;

            bool inside =
                screenPosition.x >= min.x &&
                screenPosition.x <= max.x &&
                screenPosition.y >= min.y &&
                screenPosition.y <= max.y;

            if (inside)
                AddToSelection(unit);
        }
    }

    private void AddToSelection(UnitSelection unit)
    {
        if (_selectedUnits.Add(unit))
            unit.SetSelected(true);
    }

    private void ClearSelection()
    {
        foreach (UnitSelection unit in _selectedUnits)
        {
            if (unit != null)
                unit.SetSelected(false);
        }

        _selectedUnits.Clear();
    }

    private void OnGUI()
    {
        if (!_isDragging)
            return;

        Rect rect = GetScreenRect(_dragStart, _currentMousePosition);

        rect.y = Screen.height - rect.y - rect.height;

        Color previousColor = GUI.color;

        GUI.color = new Color(1f, 1f, 1f, 0.18f);

        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        GUI.color = Color.white;

        const float borderWidth = 1f;

        GUI.DrawTexture(
            new Rect(rect.x, rect.y, rect.width, borderWidth),
            Texture2D.whiteTexture);

        GUI.DrawTexture(
            new Rect(rect.x, rect.yMax - borderWidth, rect.width, borderWidth),
            Texture2D.whiteTexture);

        GUI.DrawTexture(
            new Rect(rect.x, rect.y, borderWidth, rect.height),
            Texture2D.whiteTexture);

        GUI.DrawTexture(
            new Rect(rect.xMax - borderWidth, rect.y, borderWidth, rect.height),
            Texture2D.whiteTexture);

        GUI.color = previousColor;
    }

    private static Rect GetScreenRect(Vector2 a, Vector2 b)
    {
        return Rect.MinMaxRect(
            Mathf.Min(a.x, b.x),
            Mathf.Min(a.y, b.y),
            Mathf.Max(a.x, b.x),
            Mathf.Max(a.y, b.y));
    }
}