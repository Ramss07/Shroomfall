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
    float visualYawDeg;

    // Input sampling
    Vector2 moveInputVector = Vector2.zero;
    Vector2 lookDelta = Vector2.zero;
    bool isJumpButtonPressed = false;
    bool isAwakeButtonPressed = false;
    bool isGrabButtonPressed = false;

    // Controller settings
    float maxSpeed = 3;

    // States
    bool isGrounded = false;
    bool isActiveRagdoll = true;
    public bool IsActiveRagdoll => isActiveRagdoll;
    bool isGrabbingActive = false;
    public bool IsGrabbingActive => isGrabbingActive;

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

    //Grabhandler
    HandGrabHandler[] handGrabHandlers;

    void Awake()
    {
        syncPhysicsObjects = GetComponentsInChildren<SyncPhysicsObject>();
        handGrabHandlers = GetComponentsInChildren<HandGrabHandler>();

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
            moveInputVector.x = Input.GetAxis("Horizontal");
            moveInputVector.y = Input.GetAxis("Vertical");

            // Mouse (accumulate; Fusion will drain once per tick)
            lookDelta.x -= Input.GetAxisRaw("Mouse X");
            lookDelta.y += Input.GetAxisRaw("Mouse Y");
            visualYawDeg += Input.GetAxisRaw("Mouse X") * mouseXSens;
            visualYawDeg = Mathf.DeltaAngle(0f, visualYawDeg);

            if (Input.GetKeyDown(KeyCode.Space))
                isJumpButtonPressed = true;

            if (Input.GetKeyDown(KeyCode.F))
                isAwakeButtonPressed = true;

            isGrabButtonPressed = Input.GetKey(KeyCode.G);
        }
    }

    public override void FixedUpdateNetwork()
    {
        Vector3 localVelocifyVsForward = Vector3.zero;
        float localForwardVelocity = 0;


            // Grounding
            isGrounded = false;
            int numberOfHits = Physics.SphereCastNonAlloc(
                rigidbody3D.position, 0.1f, -transform.up, raycastHits, 0.5f
            );
            for (int i = 0; i < numberOfHits; i++) {
                if (raycastHits[i].transform.root == transform) continue;
                isGrounded = true; break;
            }
            if (!isGrounded) rigidbody3D.AddForce(Vector3.down * 10f, ForceMode.Acceleration);

            localVelocifyVsForward = transform.forward * Vector3.Dot(transform.forward, rigidbody3D.linearVelocity);
            localForwardVelocity = localVelocifyVsForward.magnitude;
        

        if (GetInput(out NetworkInputData networkInputData))
        {

            // Mouse look
            if (isActiveRagdoll)
            {
                yawDeg += networkInputData.lookDelta.x * mouseXSens;
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
                    cameraAnchor.localRotation = Quaternion.Euler(pitchDeg, 0f, 0f);
                }
            }

            // Movement & jump
            float inputMagnitude = networkInputData.movementInput.magnitude;
            isGrabbingActive = networkInputData.isGrabPressed;

            if (!IsDead && isActiveRagdoll)
            {
                // Use the client’s aim for the move frame instead of cameraAnchor/body yaw
                Quaternion aimRot = Quaternion.Euler(0f, networkInputData.aimYawDeg, 0f);

                Vector3 fwd = aimRot * Vector3.forward; fwd.y = 0f; fwd.Normalize();
                Vector3 right = aimRot * Vector3.right; right.y = 0f; right.Normalize();

                Vector3 moveDir = fwd * networkInputData.movementInput.y
                                + right * networkInputData.movementInput.x;

                if (moveDir.sqrMagnitude > 0.0001f)
                {
                    moveDir.Normalize();
                    Vector3 vel = rigidbody3D.linearVelocity; vel.y = 0f;
                    if (vel.magnitude < maxSpeed)
                        rigidbody3D.AddForce(moveDir * 30f, ForceMode.Acceleration);
                }

                // Jump
                if (isGrounded && networkInputData.isJumpPressed)
                {
                    rigidbody3D.AddForce(Vector3.up * 30f, ForceMode.Impulse);
                    isJumpButtonPressed = false;
                }
                // Dampen to reduce sliding
                if (networkInputData.movementInput.sqrMagnitude < 0.0001f && isGrounded)
                {
                    Vector3 horizVel = rigidbody3D.linearVelocity;
                    horizVel.y = 0f;
                    rigidbody3D.AddForce(-horizVel * 3f, ForceMode.Acceleration);
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
            // Auto-respawn if we fall out of the world (Temporary solution)
            if (transform.position.y < -10)
            {
                networkRigidbody3D.Teleport(Vector3.zero, Quaternion.identity);
            }

            foreach (HandGrabHandler handGrabHandler in handGrabHandlers)
            {
                handGrabHandler.UpdateState();
            }
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

        if (Object.HasInputAuthority && cameraAnchor)
        {
            float simYawDeg = transform.eulerAngles.y;
            float yawOffset = Mathf.DeltaAngle(simYawDeg, visualYawDeg); // how far ahead the camera is

            cameraAnchor.localRotation = Quaternion.Euler(pitchDeg, yawOffset, 0f);

            cinemachineBrain.ManualUpdate();
            cinemachineVirtualCamera.UpdateCameraState(Vector3.up, Runner.LocalAlpha);
        }
    }

    public NetworkInputData GetNetworkInput()
    {
        NetworkInputData networkInputData = new NetworkInputData();

        networkInputData.movementInput = moveInputVector;
        networkInputData.lookDelta     = lookDelta;
        networkInputData.aimYawDeg     = visualYawDeg;

        if (isJumpButtonPressed) networkInputData.isJumpPressed = true;
        if (isAwakeButtonPressed)  networkInputData.isAwakeButtonPressed = true;
        if (isGrabButtonPressed) networkInputData.isGrabPressed = true;

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
        isGrabbingActive = false;
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
        isGrabbingActive = false;
    }

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            Local = this;
            cinemachineVirtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
            cinemachineBrain = FindObjectOfType<CinemachineBrain>();
            if (cameraAnchor != null) {
            cinemachineVirtualCamera.m_Follow = cameraAnchor;
            cinemachineVirtualCamera.m_LookAt = cameraAnchor;
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

  transform.name = $"P_{Object.Id}";

  // PROXIES ONLY: disable physics
  if (!Object.HasStateAuthority && !Object.HasInputAuthority) {
        // Pure proxy
            if (mainJoint) Destroy(mainJoint);
            rigidbody3D.isKinematic = true;
        } else {
            // Host or Local Client: must simulate physics for prediction
            rigidbody3D.isKinematic = false;
            rigidbody3D.interpolation = RigidbodyInterpolation.None; // let Fusion drive timing
        }

        var shroom = GetComponentInChildren<ShroomCustomizerMPB>(true);
        if (shroom) shroom.Reapply();

        if (Object.HasStateAuthority) {
            if (Hp <= 0) { Hp = maxHp; IsDead = false; }
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
