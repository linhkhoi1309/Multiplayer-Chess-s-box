using System;
using Sandbox;

public class PlayerGrab : Component
{
	/// <summary>
	/// Input Action name for grabbing an object
	/// </summary>
    [Property] public string GrabActionName { get; set; } = "Attack1";
	/// <summary>
	/// Input Action name for rotating a held object
	/// </summary>
	[Property] public string RotationActionName { get; set; } = "reload";
	/// <summary>
	/// Interact tag to search gameobjects for
	/// </summary>
    [Property] public string TagName { get; set; } = "grab";
	/// <summary>
	/// Length of the ray for raycast
	/// </summary>
    [Property] public float RayLength { get; set; } = 125.0f;
	/// <summary>
	/// Base rotation speed factor
	/// </summary>
    [Property, MinMax(0f, 1.0f)] public float RotationSpeedFactor { get; set; } = 0.5f;
	/// <summary>
	/// Minimum mass factor for rotating a held object
	/// </summary>
    [Property] public float MinRotationMassFactor { get; set; } = 0.1f;
    public event Action<GameObject> OnObjectGrab;
    public event Action<GameObject> OnObjectRelease;
    public event Action<GameObject> OnObjectRotate;
    public bool IsHoldingObject;
    private GameObject _grabbedObject;
    private Rigidbody _grabbedBody;
    private CameraComponent _camera;
    private PlayerController _playerController;
    private SceneTraceResult _trace;
    private Rotation _targetRotation;
    private bool _isRotating;
    private float _grabDistance;

    protected override void OnStart()
    {
        base.OnStart();

        _playerController = GetComponentInChildren<PlayerController>();
        if ( _playerController == null )
        {
			Log.Error( "Failed to get PlayerController" );
			return;
        }

        _camera = _playerController.UseCameraControls ? Scene.Camera : GetComponentInChildren<CameraComponent>();
        if ( _camera == null )
        {
            Log.Error( "Failed to get CameraComponent" );
            return;
        }

    }

	protected override void OnUpdate()
    {
        if ( StartedGrab() )
        {
            _trace = CastRay();

            if ( _trace.Hit && _trace.GameObject.Tags.Has( TagName ) )
            {
                GrabObject( _trace.GameObject );
            }
        }

        if ( ReleasedGrab() )
        {
            ReleaseObject();
        }

        _isRotating = Input.Down( RotationActionName );

        if ( IsHoldingObject && _isRotating )
        {
	        _playerController.UseLookControls = false;

			float mass = _grabbedBody.Mass.Clamp( 1f, 50f );
			float massFactor = MathF.Max( 1.0f / MathF.Sqrt( mass ), MinRotationMassFactor );
            float yaw = Input.MouseDelta.x * RotationSpeedFactor * massFactor;
            float pitch = Input.MouseDelta.y * RotationSpeedFactor * massFactor;
            Rotation camRotation = _camera.WorldRotation;

            _targetRotation = Rotation.FromAxis( camRotation.Up, yaw ) *
                              Rotation.FromAxis( camRotation.Right, pitch ) *
                              _targetRotation;

			OnObjectRotate?.Invoke( _grabbedObject );

        }

        if ( IsHoldingObject )
        {
            UpdateGrabbedObjectPosition();
        }

        if ( !_isRotating && !_playerController.UseLookControls )
        {
	        _playerController.UseLookControls = true;
        }
    }

    private bool StartedGrab() => Input.Pressed( GrabActionName ) && !IsHoldingObject;
    private bool ReleasedGrab() => Input.Released( GrabActionName ) && IsHoldingObject;

    private void GrabObject( GameObject gameObject )
    {
        if ( gameObject == null )
        {
            Log.Warning("[PlayerGrab] No object to grab");
            return;
        }

        var body = gameObject.Components.Get<Rigidbody>();
        if ( body == null )
        {
            Log.Warning($"[PlayerGrab] No Rigidbody on object");
            return;
        }

        _grabbedObject = gameObject;
        _grabbedBody = body;
        _grabDistance = Vector3.DistanceBetween(
            _camera.WorldPosition,
            _grabbedObject.WorldPosition
        );

        _targetRotation = _grabbedObject.WorldRotation;
        IsHoldingObject = true;

        OnObjectGrab?.Invoke( _grabbedObject );
        RequestGrab( _grabbedObject );
    }

    private void ReleaseObject()
    {
        RequestRelease( _grabbedObject );
        OnObjectRelease?.Invoke( _grabbedObject );

        IsHoldingObject = false;
        _grabbedObject = null;
        _grabbedBody = null;
        _trace = new SceneTraceResult();
    }

    [Rpc.Broadcast]
    private void RequestGrab(GameObject gameObject)
    {
        gameObject.Network.AssignOwnership(Rpc.Caller);
    }

    [Rpc.Broadcast]
    private void RequestRelease(GameObject gameObject)
    {
        gameObject.Network.AssignOwnership(Connection.Host);
    }

    private void UpdateGrabbedObjectPosition()
    {
        if ( _grabbedBody == null ) return;

        Vector3 targetPos =
            _camera.WorldPosition +
            _camera.WorldRotation.Forward * _grabDistance;

        float massFactor = _grabbedBody.Mass.Clamp( 1f, 50f );
        float arriveTime = 0.05f + (massFactor * 0.01f);
        Transform targetTransform = new Transform( targetPos, _targetRotation );

        _grabbedBody.SmoothMove(
            targetTransform,
            arriveTime,
            Time.Delta
        );
    }

    private SceneTraceResult CastRay()
    {
        Vector3 direction =
            (_camera.ScreenToWorld( Screen.Size * 0.5f ) - _camera.WorldPosition).Normal;

        Vector3 start = GameObject.WorldPosition + new Vector3( 0.0f, 0.0f, 64.0f );
        Vector3 end = start + direction * RayLength;

        return Scene.Trace.Ray( start, end )
            .IgnoreGameObjectHierarchy( GameObject )
            .UseHitboxes()
            .Run();
    }
}
