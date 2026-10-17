using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class UnitSelectionController : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField, Min(1f)] private float dragThreshold = 8f;

    private readonly HashSet<UnitGroup> _selectedGroups = new();

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
            if (!TryMergeWithGroupAtMousePosition())
                IssueMoveCommandToSelectedGroups();
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
                SelectGroupsInRectangle();
            else
                SelectGroupAtMousePosition();

            _isDragging = false;
        }
    }

    private bool TryMergeWithGroupAtMousePosition()
    {
        if (_selectedGroups.Count == 0)
            return false;

        Ray ray = worldCamera.ScreenPointToRay(
            Mouse.current.position.ReadValue());

        RaycastHit2D hit = Physics2D.GetRayIntersection(ray);

        if (hit.collider == null)
            return false;

        UnitGroupMember targetMember =
            hit.collider.GetComponentInParent<UnitGroupMember>();

        if (targetMember == null || targetMember.Group == null)
            return false;

        UnitGroup sourceGroup = null;

        foreach (UnitGroup group in _selectedGroups)
        {
            if (group != null)
            {
                sourceGroup = group;
                break;
            }
        }

        if (sourceGroup == null)
            return true;

        UnitGroup targetGroup = targetMember.Group;

        bool merged = UnitGroupManager.Instance != null &&
                      UnitGroupManager.Instance.MergeNewestMember(
                          sourceGroup,
                          targetGroup);

        if (merged)
        {
            ClearSelection();
            AddToSelection(targetGroup);
        }

        return true;
    }

    private void IssueMoveCommandToSelectedGroups()
    {
        if (_selectedGroups.Count == 0)
            return;

        Ray ray = worldCamera.ScreenPointToRay(
            Mouse.current.position.ReadValue());

        Plane worldPlane = new Plane(Vector3.forward, Vector3.zero);

        if (!worldPlane.Raycast(ray, out float distance))
            return;

        Vector3 destination = ray.GetPoint(distance);

        foreach (UnitGroup group in _selectedGroups)
        {
            if (group == null)
                continue;

            UnitGroupMember leader = group.GetLeader();

            if (leader == null)
                continue;

            Vector3 originalCenter = leader.transform.position;

            foreach (UnitGroupMember member in group.Members)
            {
                if (member == null)
                    continue;

                UnitMovement movement =
                    member.GetComponent<UnitMovement>();

                if (movement == null)
                    continue;

                Vector3 relativeOffset =
                    member.transform.position - originalCenter;

                Vector2 memberDestination = new Vector2(
                    destination.x + relativeOffset.x,
                    destination.y + relativeOffset.y);

                movement.MoveTo(memberDestination);
            }
        }
    }

    private void SelectGroupAtMousePosition()
    {
        ClearSelection();

        Ray ray = worldCamera.ScreenPointToRay(_currentMousePosition);
        RaycastHit2D hit = Physics2D.GetRayIntersection(ray);

        if (hit.collider == null)
            return;

        UnitGroupMember member =
            hit.collider.GetComponentInParent<UnitGroupMember>();

        if (member != null && member.Group != null)
            AddToSelection(member.Group);
    }

    private void SelectGroupsInRectangle()
    {
        ClearSelection();

        Vector2 min = Vector2.Min(_dragStart, _currentMousePosition);
        Vector2 max = Vector2.Max(_dragStart, _currentMousePosition);

        UnitGroupMember[] allMembers =
            FindObjectsByType<UnitGroupMember>(
                FindObjectsSortMode.None);

        foreach (UnitGroupMember member in allMembers)
        {
            if (member == null || !member.isActiveAndEnabled)
                continue;

            UnitGroup group = member.Group;

            if (group == null)
                continue;

            Vector3 screenPosition =
                worldCamera.WorldToScreenPoint(member.transform.position);

            if (screenPosition.z < 0f)
                continue;

            bool inside =
                screenPosition.x >= min.x &&
                screenPosition.x <= max.x &&
                screenPosition.y >= min.y &&
                screenPosition.y <= max.y;

            if (inside)
                AddToSelection(group);
        }
    }

    private void AddToSelection(UnitGroup group)
    {
        if (group != null && _selectedGroups.Add(group))
            group.SetSelected(true);
    }

    private void ClearSelection()
    {
        foreach (UnitGroup group in _selectedGroups)
        {
            if (group != null)
                group.SetSelected(false);
        }

        _selectedGroups.Clear();
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