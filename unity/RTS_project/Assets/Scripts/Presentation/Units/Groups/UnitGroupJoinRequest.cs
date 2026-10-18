
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UnitGroupJoinRequest : MonoBehaviour
{
    [Header("Joining")]
    [SerializeField, Min(0.05f)]
    private float retryInterval = 0.5f;

    [SerializeField, Min(0.01f)]
    private float positionTolerance = 0.12f;

    private UnitGroup _joiningGroup;
    private UnitGroup _targetGroup;
    private UnitGroupMember _member;
    private UnitMovement _movement;

    private bool _hasCandidatePosition;
    private Vector3 _candidatePosition;
    private float _nextRetryTime;
    private bool _cancelled;

    public void Initialize(UnitGroup targetGroup)
    {
        _joiningGroup = GetComponent<UnitGroup>();
        _targetGroup = targetGroup;

        if (_joiningGroup == null || _targetGroup == null)
        {
            CancelJoin();
            return;
        }

        _member = _joiningGroup.GetLeader();

        if (_member == null)
        {
            CancelJoin();
            return;
        }

        _movement = _member.GetComponent<UnitMovement>();

        if (_movement == null)
        {
            CancelJoin();
            return;
        }

        RequestCurrentSlot();
    }

    private void Update()
    {
        if (_cancelled)
            return;

        if (_joiningGroup == null ||
            _targetGroup == null ||
            _member == null ||
            _movement == null)
        {
            CancelJoin();
            return;
        }

        if (!_hasCandidatePosition)
        {
            if (Time.time >= _nextRetryTime)
                RequestCurrentSlot();

            return;
        }

        // Amíg úton van, a kapott célpont felé halad.
        if (_movement.IsMoving)
            return;

        // Amikor megállt, újra lekérdezi a célcsoportot.
        RequestCurrentSlot();
    }

    private void RequestCurrentSlot()
    {
        if (_cancelled)
            return;

        if (_targetGroup == null ||
            !_targetGroup.TryGetJoinSlot(
                _member,
                out _,
                out Vector3 currentSlotPosition))
        {
            // B nem tárol semmit. T1 késõbb újra megkérdezi.
            _hasCandidatePosition = false;
            _nextRetryTime = Time.time + retryInterval;
            return;
        }

        _candidatePosition = currentSlotPosition;
        _hasCandidatePosition = true;

        Vector2 currentPosition = _member.transform.position;
        Vector2 requestedPosition = _candidatePosition;

        float distance = Vector2.Distance(
            currentPosition,
            requestedPosition);

        if (distance > positionTolerance)
        {
            _movement.MoveTo(requestedPosition);
            return;
        }

        // A hely megfelelõnek tûnik, de a manager is
        // újraellenõrzi a feltételeket a tényleges felvétel elõtt.
        bool joined =
            UnitGroupManager.Instance != null &&
            UnitGroupManager.Instance.TryCompleteJoin(
                _joiningGroup,
                _targetGroup,
                _member,
                positionTolerance);

        if (joined)
        {
            Destroy(this);
            return;
        }

        // B állapota vagy a szabad slot idõközben megváltozhatott.
        _hasCandidatePosition = false;
        _nextRetryTime = Time.time + retryInterval;
    }

    /// <summary>
    /// A játékos új parancsot adott a csatlakozó egységnek.
    /// A célcsoportot ez semmilyen formában nem érinti.
    /// </summary>
    public void CancelJoin()
    {
        if (_cancelled)
            return;

        _cancelled = true;
        _hasCandidatePosition = false;

        if (_movement != null)
            _movement.StopMovement();

        Destroy(this);
    }
}
