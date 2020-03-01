using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeController : MonoBehaviour
{
    #region Data Structures

    private enum Anchor
    {
        BottomFront,
        BottomBack,
        BottomLeft,
        BottomRight,
        TopFront,
        TopBack,
        TopLeft,
        TopRight
    }

    private enum Direction
    {
        Forward,
        Backward,
        Right,
        Left
    }

    private struct Rotation
    {
        public Anchor anchor;
        public Vector3 axis;
        public Vector3 direction;

        public Rotation(Anchor anchor, Vector3 axis, Vector3 direction)
        {
            this.anchor = anchor;
            this.axis = axis;
            this.direction = direction;
        }
    }

    private struct RaycastInfo
    {
        public bool forward;
        public bool up;
        public bool down;
        public bool bottomDown;

        public RaycastInfo(bool forward, bool up, bool down, bool bottomDown)
        {
            this.forward = forward;
            this.up = up;
            this.down = down;
            this.bottomDown = bottomDown;
        }
    }

    #endregion Data Structures

    [SerializeField]
    private BoxCollider _collider = null;

    [SerializeField]
    private LayerMask _obstacleLayers = new LayerMask();

    [SerializeField]
    private float _rotationDuration = 0f;

    private bool _isRotating = false;

    private Dictionary<KeyCode, Direction> _inputs = null;
    private Dictionary<Anchor, Vector3> _localAnchors = null;
    private Dictionary<Direction, Rotation> _rotations = null;

    private static float _obstacleCheckDistance = 0.75f;

    private void Awake()
    {
        InitializeDictionaries();
    }

    private void Update()
    {
        if (_inputs != null)
        {
            foreach (var input in _inputs)
            {
                if (Input.GetKeyDown(input.Key) && !_isRotating)
                {
                    StartCoroutine(Rotate(input.Value));
                }
            }
        }
    }

    private void InitializeDictionaries()
    {
        Bounds bounds = _collider.bounds;

        _localAnchors = new Dictionary<Anchor, Vector3>()
        {
            { Anchor.BottomFront, new Vector3(0, -bounds.size.y / 2, bounds.size.z / 2) },
            { Anchor.BottomBack, new Vector3(0, -bounds.size.y / 2, -bounds.size.z / 2) },
            { Anchor.BottomRight, new Vector3(bounds.size.x / 2, -bounds.size.y / 2, 0) },
            { Anchor.BottomLeft, new Vector3(-bounds.size.x / 2, -bounds.size.y / 2, 0) },
            { Anchor.TopFront, new Vector3(0, bounds.size.y / 2, bounds.size.z / 2) },
            { Anchor.TopBack, new Vector3(0, bounds.size.y / 2, -bounds.size.z / 2) },
            { Anchor.TopRight, new Vector3(bounds.size.x / 2, bounds.size.y / 2, 0) },
            { Anchor.TopLeft, new Vector3(-bounds.size.x / 2, bounds.size.y / 2, 0) }
        };

        _rotations = new Dictionary<Direction, Rotation>()
        {
            { Direction.Forward, new Rotation(Anchor.BottomFront, Vector3.right, Vector3.forward) },
            { Direction.Backward, new Rotation(Anchor.BottomBack, -Vector3.right, Vector3.back) },
            { Direction.Right, new Rotation(Anchor.BottomRight, -Vector3.forward, Vector3.right) },
            { Direction.Left, new Rotation(Anchor.BottomLeft, Vector3.forward, Vector3.left) }
        };

        _inputs = new Dictionary<KeyCode, Direction>()
        {
            { KeyCode.W, Direction.Forward },
            { KeyCode.S, Direction.Backward },
            { KeyCode.A, Direction.Left },
            { KeyCode.D, Direction.Right },
        };
    }

    private RaycastInfo CheckForObstacle(Direction direction)
    {
        RaycastInfo info = new RaycastInfo();
        Vector3 rayDir = _rotations[direction].direction;

        /// Forward
        Ray ray = new Ray(transform.position, rayDir);
        Debug.DrawLine(ray.origin, ray.origin + (rayDir * _obstacleCheckDistance), Color.red, 1);
        info.forward = Physics.Raycast(ray, _obstacleCheckDistance, _obstacleLayers);

        return info;
    }

    private void CheckObstacleDown(Direction direction, ref RaycastInfo info)
    {
        /// Down
        Ray ray = new Ray(transform.position + _rotations[direction].direction, Vector3.down);
        Debug.DrawRay(ray.origin, Vector3.down * _obstacleCheckDistance, Color.red, 1);
        info.down = Physics.Raycast(ray, _obstacleCheckDistance, _obstacleLayers);

        /// Bottom
        ray.origin += Vector3.down;
        info.bottomDown = info.down | Physics.Raycast(ray, _obstacleCheckDistance, _obstacleLayers);
        Debug.DrawRay(ray.origin, Vector3.down * _obstacleCheckDistance, Color.red, 1);
    }

    private void CheckObstacleUp(Direction direction, ref RaycastInfo info)
    {
        Ray ray = new Ray(transform.position + Vector3.up, _rotations[direction].direction);
        info.up = Physics.Raycast(ray, _obstacleCheckDistance, _obstacleLayers);
    }

    private IEnumerator Rotate(Direction direction)
    {
        Vector3 position = transform.position;
        Rotation rotation = _rotations[direction];
        Vector3 anchor = _localAnchors[rotation.anchor];
        float angle = 90f;

        RaycastInfo raycastHits = CheckForObstacle(direction);

        if (raycastHits.forward)
        {
            CheckObstacleUp(direction, ref raycastHits);

            if (raycastHits.up)
            {
                yield break;
            }
            else
            {
                anchor = _localAnchors[rotation.anchor + 4];
                angle = 180f;
            }
        }
        else
        {
            CheckObstacleDown(direction, ref raycastHits);

            if (!raycastHits.down && !raycastHits.bottomDown)
            {
                yield break;
            }
            else if (!raycastHits.down && raycastHits.bottomDown)
            {
                angle = 180f;
            }
        }

        _isRotating = true;

        float elapsed = 0f;
        while (elapsed < _rotationDuration)
        {
            elapsed += Time.deltaTime;
            transform.RotateAround(position + anchor, rotation.axis, (angle / _rotationDuration) * Time.deltaTime);
            yield return null;
        }

        SnapPosition();

        _isRotating = false;
    }

    private void SnapPosition()
    {
        transform.position = transform.position.Snap(0.5f);
        transform.eulerAngles = transform.eulerAngles.Snap(90f);
    }

    #region Editor

    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying && _localAnchors != null)
        {
            foreach (Vector3 anchor in _localAnchors.Values)
            {
                Gizmos.DrawWireCube(transform.position + anchor, new Vector3(0.1f, 0.1f, 0.1f));
            }
        }
    }

    #endregion Editor
}
