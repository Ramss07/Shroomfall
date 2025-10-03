using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using Fusion.Addons.Physics;

public class NetworkPlayer : NetworkBehaviour, IPlayerLeft
{
    public static NetworkPlayer Local { get; set; }

    [SerializeField] Rigidbody rigidbody3D;
    [SerializeField] NetworkRigidbody3D networkRigidbody3D;
    [SerializeField] ConfigurableJoint mainJoint;
    [SerializeField] Animator animator;
    [SerializeField] Transform cameraAnchor;


    //Health
    [SerializeField] private int maxHp = 100;
    [Networked] public int Hp { get; set; }
    [Networked] public NetworkBool IsDead { get; set; }
    public float HpPercent => maxHp <= 0 ? 0f : (float)Hp / maxHp;

    //Mouse look
    [Header("Look Settings")]
    [SerializeField] float mouseXSens = 2.5f;          // yaw sensitivity
    [SerializeField] float mouseYSens = 2.0f;          // pitch sensitivity
    [SerializeField] float minPitch = -60f;            // look down limit
    [SerializeField] float maxPitch = 70f;             // look up limit
    [SerializeField] ConfigurableJoint headJoint;

    // Runtime aim state
    float yawDeg;
    float pitchDeg;
    Quaternion headStartLocalRot;

    // Input sampling
    Vector2 moveInputVector = Vector2.zero;
    Vector2 lookDelta = Vector2.zero;
    bool isJumpButtonPressed = false;
    bool isAwakeButtonPressed = false;

    // Controller settings
    float maxSpeed = 3;

    // States
    bool isGrounded = false;
    bool isActiveRagdoll = true;
    public bool IsActiveRagdoll => isActiveRagdoll;

    // Raycasts
    RaycastHit[] raycastHits = new RaycastHit[10];

    // Syncing of physics objects
    SyncPhysicsObject[] syncPhysicsObjects;

    // Cinemachine
    CinemachineVirtualCamera cinemachineVirtualCamera;
    CinemachineBrain cinemachineBrain;

    // Syncing clients ragdolls
    [Networked, Capacity(10)] public NetworkArray<Quaternion> networkPhysicsSyncedRotations { get; }

    // Store original values
    float startSlerpPositionSpring = 0.0f;

    // Timing
    float lastTimeBecameRagdoll = 0;

    void Awake()
    {
        syncPhysicsObjects = GetComponentsInChildren<SyncPhysicsObject>();
        if (!mainJoint) mainJoint = GetComponent<ConfigurableJoint>();

        // Auto-pick a head/neck joint if not assigned
        if (!headJoint)
        {
            foreach (var j in GetComponentsInChildren<ConfigurableJoint>(true))
            {
                string n = j.name.ToLowerInvariant();
                if (n.Contains("head") || n.Contains("neck")) { headJoint = j; break; }
            }
        }
    }

    void Start()
    {
        // Store original main joint spring for restore
        startSlerpPositionSpring = mainJoint.slerpDrive.positionSpring;
        if (headJoint) headStartLocalRot = headJoint.transform.localRotation;
    }

    void Update()
    {
        // ---- InputAuthority only: sample raw inputs ----
        if (Object.HasInputAuthority)
        {
            // WASD (movement wiring comes later)
            moveInputVector.x = Input.GetAxis("Horizontal");
            moveInputVector.y = Input.GetAxis("Vertical");

            // Mouse (accumulate; Fusion will drain once per tick)
            lookDelta.x += Input.GetAxisRaw("Mouse X");
            lookDelta.y += Input.GetAxisRaw("Mouse Y");

            if (Input.GetKeyDown(KeyCode.Space))
                isJumpButtonPressed = true;

            if (Input.GetKeyDown(KeyCode.Space))
                isAwakeButtonPressed = true;
        }
    }

    public override void FixedUpdateNetwork()
    {
        Vector3 localVelocifyVsForward = Vector3.zero;
        float localForwardVelocity = 0;

        if (Object.HasStateAuthority)
        {
            // Grounding
            isGrounded = false;
            int numberOfHits = Physics.SphereCastNonAlloc(rigidbody3D.position, 0.1f, transform.up * -1, raycastHits, 0.5f);
            for (int i = 0; i < numberOfHits; i++)
            {
                if (raycastHits[i].transform.root == transform) continue;
                isGrounded = true;
                break;
            }

            if (!isGrounded)
                rigidbody3D.AddForce(Vector3.down * 10);

            localVelocifyVsForward = transform.forward * Vector3.Dot(transform.forward, rigidbody3D.linearVelocity);
            localForwardVelocity = localVelocifyVsForward.magnitude;
        }

        if (GetInput(out NetworkInputData networkInputData))
        {
            if (!Object.HasStateAuthority) { isJumpButtonPressed = false; return; }

            // Mouse look
            if (isActiveRagdoll)
            {
                yawDeg -= networkInputData.lookDelta.x * mouseXSens;
                pitchDeg -= networkInputData.lookDelta.y * mouseYSens;
                pitchDeg = Mathf.Clamp(pitchDeg, minPitch, maxPitch);

                if (mainJoint)
                {
                    Vector3 desiredFwd = Quaternion.Euler(0f, yawDeg, 0f) * Vector3.forward;
                    Quaternion desiredYawRot = Quaternion.LookRotation(desiredFwd, Vector3.up);

                    mainJoint.targetRotation = Quaternion.RotateTowards(
                        mainJoint.targetRotation, desiredYawRot, Runner.DeltaTime * 720f);
                }

                if (headJoint)
                {
                    headJoint.targetRotation = headStartLocalRot * Quaternion.Euler(-pitchDeg, 0f, 0f);
                }
                if (cameraAnchor != null)
                {
                    cameraAnchor.localRotation = Quaternion.Euler(pitchDeg, cameraAnchor.localRotation.eulerAngles.y, 0f);
                }
            }

            // Movement & jump
            float inputMagnitude = networkInputData.movementInput.magnitude;

            if (!IsDead && isActiveRagdoll)
            {
                if (inputMagnitude > 0.001f)
                {
                    Vector3 move = new Vector3(networkInputData.movementInput.x, 0f, networkInputData.movementInput.y * -1f);
                    Quaternion desiredDirection = Quaternion.LookRotation(move, transform.up);

                    if (mainJoint)
                        mainJoint.targetRotation = Quaternion.RotateTowards(
                            mainJoint.targetRotation, desiredDirection, Runner.DeltaTime * 300f);

                    if (localForwardVelocity < maxSpeed)
                        rigidbody3D.AddForce(transform.forward * inputMagnitude * 30f, ForceMode.Acceleration);
                }

                if (isGrounded && networkInputData.isJumpPressed)
                {
                    rigidbody3D.AddForce(Vector3.up * 20f, ForceMode.Impulse);
                    isJumpButtonPressed = false;
                }
            }
            else
            {
                // We are in full ragdoll. Only allow stand-up if NOT dead.
                if (!IsDead && networkInputData.isAwakeButtonPressed && Runner.SimulationTime - lastTimeBecameRagdoll > 3)
                    MakeActiveRagdoll();
            }
        }

        if (Object.HasStateAuthority)
        {
            animator.SetFloat("movementSpeed", localForwardVelocity * 0.4f);

            // Write bone rotations every tick so proxies can interpolate
            for (int i = 0; i < syncPhysicsObjects.Length; i++)
            {
                if (isActiveRagdoll)
                    syncPhysicsObjects[i].UpdateJointFromAnimation();

                networkPhysicsSyncedRotations.Set(i, syncPhysicsObjects[i].transform.localRotation);
            }

            if (transform.position.y < -10)
                networkRigidbody3D.Teleport(Vector3.zero, Quaternion.identity);
        }
    }

    public override void Render()
    {
        if (!Object.HasStateAuthority)
        {
            var interpolated = new NetworkBehaviourBufferInterpolator(this);
            for (int i = 0; i < syncPhysicsObjects.Length; i++)
            {
                syncPhysicsObjects[i].transform.localRotation =
                    Quaternion.Slerp(syncPhysicsObjects[i].transform.localRotation,
                                     networkPhysicsSyncedRotations.Get(i), interpolated.Alpha);
            }
        }

        if (Object.HasInputAuthority)
        {
            cinemachineBrain.ManualUpdate();
            cinemachineVirtualCamera.UpdateCameraState(Vector3.up, Runner.LocalAlpha);
        }
    }

    public NetworkInputData GetNetworkInput()
    {
        NetworkInputData networkInputData = new NetworkInputData();

        networkInputData.movementInput = moveInputVector;
        networkInputData.lookDelta     = lookDelta;

        if (isJumpButtonPressed)   networkInputData.isJumpPressed = true;
        if (isAwakeButtonPressed)  networkInputData.isAwakeButtonPressed = true;

        // Reset one-shots each tick
        isJumpButtonPressed  = false;
        isAwakeButtonPressed = false;
        lookDelta            = Vector2.zero;

        return networkInputData;
    }

    public void OnPlayerBodyPartHit(int damage, Vector3 impulseDir, Rigidbody hitBody)
    {
        if (!Object.HasStateAuthority || IsDead) return;

        if (hitBody) hitBody.AddForce(impulseDir, ForceMode.Impulse);

        Hp = Mathf.Max(0, Hp - damage);

        if (Hp == 0 && !IsDead)
        {
            IsDead = true;
            MakeRagdoll();
        }
    }

    void MakeRagdoll()
    {
        if (!Object.HasStateAuthority) return;

        JointDrive jointDrive = mainJoint.slerpDrive;
        jointDrive.positionSpring = 0;
        mainJoint.slerpDrive = jointDrive;

        for (int i = 0; i < syncPhysicsObjects.Length; i++)
            syncPhysicsObjects[i].MakeRagdoll();

        lastTimeBecameRagdoll = Runner.SimulationTime;
        isActiveRagdoll = false;
    }

    void MakeActiveRagdoll()
    {
        if (!Object.HasStateAuthority) return;

        JointDrive jointDrive = mainJoint.slerpDrive;
        jointDrive.positionSpring = startSlerpPositionSpring;
        mainJoint.slerpDrive = jointDrive;

        for (int i = 0; i < syncPhysicsObjects.Length; i++)
            syncPhysicsObjects[i].MakeActiveRagdoll();

        isActiveRagdoll = true;
    }

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            Local = this;

            cinemachineVirtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
            cinemachineBrain = FindObjectOfType<CinemachineBrain>();

            if (cameraAnchor != null)
            {
                cinemachineVirtualCamera.m_Follow = cameraAnchor;
                cinemachineVirtualCamera.m_LookAt = cameraAnchor;
            }

            // Helpful when testing first-person: lock and hide cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Utils.DebugLog("Spawned player with input authority");
        }
        else Utils.DebugLog("Spawned player without input authority");

        transform.name = $"P_{Object.Id}";

        if (!Object.HasStateAuthority)
        {
            Destroy(mainJoint);
            rigidbody3D.isKinematic = true;
        }

        var shroom = GetComponentInChildren<ShroomCustomizerMPB>(true);
        if (shroom) shroom.Reapply();

        if (Object.HasStateAuthority)
        {
            if (Hp <= 0)
            {
                Hp = maxHp;
                IsDead = false;
            }

            // Initialize yaw from current facing
            yawDeg = transform.eulerAngles.y;
            pitchDeg = Mathf.Clamp(pitchDeg, minPitch, maxPitch);
            if (headJoint) headStartLocalRot = headJoint.transform.localRotation;
        }
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (Object.InputAuthority == player)
            Runner.Despawn(Object);
    }
}
