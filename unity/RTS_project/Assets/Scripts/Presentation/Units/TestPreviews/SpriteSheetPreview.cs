
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class SpriteSheetPreview : MonoBehaviour
{
    [Header("Sprite sheet")]
    [SerializeField] private Sprite[] sprites;

    [Header("Playback")]
    [SerializeField, Min(0.01f)]
    private float framesPerSecond = 12f;

    [SerializeField]
    private bool playOnStart = true;

    [SerializeField]
    private bool loop = true;

    [SerializeField]
    private bool pingPong = false;

    [Header("Debug")]
    [SerializeField]
    private bool showFrameNumber = true;

    private SpriteRenderer _spriteRenderer;
    private float _frameTimer;
    private int _frameIndex;
    private int _direction = 1;
    private bool _isPlaying;

    public int FrameIndex => _frameIndex;
    public int FrameCount => sprites == null ? 0 : sprites.Length;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        _isPlaying = playOnStart;
        ApplyFrame();
    }

    private void Update()
    {
        if (!_isPlaying || FrameCount == 0)
            return;

        _frameTimer += Time.deltaTime;

        float frameDuration = 1f / framesPerSecond;

        while (_frameTimer >= frameDuration)
        {
            _frameTimer -= frameDuration;
            AdvanceFrame();
        }
    }

    private void AdvanceFrame()
    {
        if (pingPong)
        {
            _frameIndex += _direction;

            if (_frameIndex >= FrameCount - 1)
            {
                _frameIndex = FrameCount - 1;
                _direction = -1;
            }
            else if (_frameIndex <= 0)
            {
                _frameIndex = 0;
                _direction = 1;
            }
        }
        else
        {
            _frameIndex++;

            if (_frameIndex >= FrameCount)
            {
                _frameIndex = 0;

                if (!loop)
                {
                    _isPlaying = false;
                    _frameIndex = FrameCount - 1;
                }
            }
        }

        ApplyFrame();
    }

    private void ApplyFrame()
    {
        if (_spriteRenderer == null || FrameCount == 0)
            return;

        _frameIndex = Mathf.Clamp(_frameIndex, 0, FrameCount - 1);
        _spriteRenderer.sprite = sprites[_frameIndex];

        if (showFrameNumber)
            gameObject.name = $"SpriteSheetPreview ({_frameIndex})";
    }

    public void Play()
    {
        _isPlaying = true;
    }

    public void Pause()
    {
        _isPlaying = false;
    }

    public void SetFrame(int index)
    {
        if (FrameCount == 0)
            return;

        _frameIndex = Mathf.Clamp(index, 0, FrameCount - 1);
        _frameTimer = 0f;
        ApplyFrame();
    }

    public void NextFrame()
    {
        if (FrameCount == 0)
            return;

        _frameIndex = (_frameIndex + 1) % FrameCount;
        _frameTimer = 0f;
        ApplyFrame();
    }

    public void PreviousFrame()
    {
        if (FrameCount == 0)
            return;

        _frameIndex = (_frameIndex - 1 + FrameCount) % FrameCount;
        _frameTimer = 0f;
        ApplyFrame();
    }
}
