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
    [SerializeField] DeathFade playerFade;
    [Networked] public NetworkBool CanLook { get; set; }
    bool isLeftGrabButtonPressed = false;
    bool isRightGrabButtonPressed = false;

    [Networked] public NetworkBool IsLeftGrab  { get; set; }
    [Networked] public NetworkBool IsRightGrab { get; set; }


    //Stamina
    [Header("Stamina Settings")]
    [SerializeField] float maxStamina = 100f;
    [SerializeField] float sprintDrainPerSec = 10f;   // drain while sprinting
    [SerializeField] float regenPerSec       = 20f;   // regen when not sprinting
    [SerializeField] float regenDelaySeconds = 0.5f;  // delay after sprint stops before regen
    [SerializeField] float minStartStamina = 10f;   // must have this to (re)start sprint
    [SerializeField] float hangDrainPerSec = 5f;
    [Networked] public float Stamina { get; set; }
    double staminaRegenAllowedAt = 0;
    public float StaminaPercent => maxStamina <= 0f ? 0f : Stamina / maxStamina;
    
    // Controller settings
    [Header("Movement Settings")]
    float maxSpeed = 5;
    [SerializeField] float sprintMultiplier = 1.6f; // 60% faster
    bool sprintActive = false; // latched sprint state used for movement
    [SerializeField] float sprintGraceSeconds = 0.12f; // how long after releasing sprint can you still sprint
    double sprintAllowedUntil = 0; // time until which sprint is allowed

    // Air Control
    [Header("Air Control Settings")]
    [SerializeField] float airAccel = 18f;         // how quickly you can steer in air (VelocityChange units/sec)
    [SerializeField] float airBrake = 8f;          // braking when pushing opposite direction
    [SerializeField] float airMaxSpeed = 3.2f;     // cap for building speed in air (keep momentum if already higher)
    [SerializeField] float airSideBias = 1.0f;     // 1=even; lower reduces strafe authority vs forward
    bool wasGroundedLastTick = false;
    float takeoffHorizSpeed = 0f;

    // Health
    [Header("Health Settings")]
    [SerializeField] private int maxHp = 100;
    [Networked] public int Hp { get; set; }
    [Networked] public NetworkBool IsDead { get; set; }
    public float HpPercent => maxHp <= 0 ? 0f : (float)Hp / maxHp;

    //Mouse look
    [Header("Look Settings")]
    [SerializeField] float mouseXSens = 2.5f;          // yaw sensitivity
    [SerializeField] float mouseYSens = 2.0f;          // pitch sensitivity
    [SerializeField] float minPitch = -25f;            // look down limit
    [SerializeField] float maxPitch = 60f;             // look up limit
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

    // States
    bool isGrounded = false;
    bool isActiveRagdoll = true;
    public bool IsActiveRagdoll => isActiveRagdoll;
    bool isSprintHeld = false;

    double lastGroundedTime = -1;
    const double coyoteTimeSeconds = 0.15;


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

    // Grabhandler
    HandGrabHandler[] handGrabHandlers;

    void Awake()
    {
        syncPhysicsObjects = GetComponentsInChildren<SyncPhysicsObject>();
        handGrabHandlers = GetComponentsInChildren<HandGrabHandler>();

        if (!mainJoint) mainJoint = GetComponent<ConfigurableJoint>();
    }
    
    void Start()
    {
        if (Object.HasInputAuthority)
        {
            playerFade = FindObjectOfType<DeathFade>();
        }
        // Store original main joint spring for restore
        startSlerpPositionSpring = mainJoint.slerpDrive.positionSpring;
        if (headJoint) headStartLocalRot = headJoint.transform.localRotation;
    }

    void Update()
    {
        // -------------------- InputAuthority only: sample raw inputs -------------------
        if (Object.HasInputAuthority)
        {
            moveInputVector.x = Input.GetAxis("Horizontal");
            moveInputVector.y = Input.GetAxis("Vertical");

            // Mouse (accumulate; Fusion will drain once per tick)
            if(CanLook)
            {
                lookDelta.x -= Input.GetAxisRaw("Mouse X");
                lookDelta.y += Input.GetAxisRaw("Mouse Y");
                visualYawDeg += Input.GetAxisRaw("Mouse X") * mouseXSens;
                visualYawDeg = Mathf.DeltaAngle(0f, visualYawDeg);
            }

            if (Input.GetKeyDown(KeyCode.Space))
                isJumpButtonPressed = true;

            if (Input.GetKeyDown(KeyCode.F))
                isAwakeButtonPressed = true;

            isSprintHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            isLeftGrabButtonPressed  = Input.GetMouseButton(0);
            isRightGrabButtonPressed = Input.GetMouseButton(1);
        }
    }

    public override void FixedUpdateNetwork()
    {
        Vector3 localVelocifyVsForward = Vector3.zero;
        float localForwardVelocity = 0;

        // --------------- StateAuthority only: simulate physics ---------------
        isGrounded = false;
        int numberOfHits = Physics.SphereCastNonAlloc(
                rigidbody3D.position, 0.1f, -transform.up, raycastHits, 0.5f
            );
        for (int i = 0; i < numberOfHits; i++)
        {
            if (raycastHits[i].transform.root == transform) continue;
            isGrounded = true; break;
        }
            if (isGrounded) lastGroundedTime = Runner.SimulationTime; // remember last grounded time
            if (!isGrounded) rigidbody3D.AddForce(Vector3.down * 10f, ForceMode.Acceleration); // extra gravity when airborne

            localVelocifyVsForward = transform.forward * Vector3.Dot(transform.forward, rigidbody3D.linearVelocity);
            localForwardVelocity = localVelocifyVsForward.magnitude;
        if (isGrounded)
        {
            sprintAllowedUntil = Runner.SimulationTime + sprintGraceSeconds;
        }
        Vector3 horizNowV = new Vector3(rigidbody3D.linearVelocity.x, 0f, rigidbody3D.linearVelocity.z);

        if (wasGroundedLastTick && !isGrounded) {
            // Took off this tick: remember horizontal speed
            takeoffHorizSpeed = horizNowV.magnitude;
        } else if (!wasGroundedLastTick && isGrounded) {
            // Landed: reset
            takeoffHorizSpeed = 0f;
        }
        wasGroundedLastTick = isGrounded;

        if (GetInput(out NetworkInputData networkInputData))
        {
            float df = (float)Runner.DeltaTime;

            //----------------------Mouse look-----------------------
            if (CanLook)
            {
                yawDeg += networkInputData.lookDelta.x * mouseXSens;
                pitchDeg -= networkInputData.lookDelta.y * mouseYSens;
                pitchDeg = Mathf.Clamp(pitchDeg, minPitch, maxPitch);
                /**if (Object.HasInputAuthority)
                {
                    Debug.Log($"[PITCH DEBUG] Time={Time.time:F2} | pitchDeg={pitchDeg:F2} | lookDeltaY={networkInputData.lookDelta.y:F2}");
                }**/
                
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

            //-------------------------Stamina & Sprinting---------------------------
            bool hasStartStamina = Stamina >= minStartStamina;
            bool wantsSprint = networkInputData.isSprinting && networkInputData.movementInput.sqrMagnitude > 0.01f;

            if (isGrounded || Runner.SimulationTime <= sprintAllowedUntil)
            {
                if(!sprintActive)
                {
                    sprintActive = wantsSprint && hasStartStamina;
                }
                else
                {
                    // already sprinting: can keep sprinting as long as held and have stamina
                    sprintActive = wantsSprint && Stamina > 0f;
                }
            }
            else
            {
                // in air outside grace: can only KEEP sprint if already active and still held
                sprintActive = sprintActive && networkInputData.isSprinting;
            }

            // Stamina drain / regen
            float dt = (float)Runner.DeltaTime;

            bool sprintDrain = isGrounded
                   && sprintActive
                   && networkInputData.isSprinting
                   && networkInputData.movementInput.sqrMagnitude > 0.01f;

            bool isHanging = !isGrounded
                   && (handGrabHandlers[0].IsLatchedToKinematic
                   || handGrabHandlers[1].IsLatchedToKinematic);


            if (isHanging)
            {
                // drain while hanging (not grounded with a latched hand)
                Stamina = Mathf.Max(0f, Stamina - hangDrainPerSec * dt);
                staminaRegenAllowedAt = Runner.SimulationTime + regenDelaySeconds;
                if (Stamina <= 0f)
                {
                    foreach (var h in handGrabHandlers) h.ReleaseIfLatched();
                }
            }

            else if (sprintDrain)
            {
                // drain while sprinting
                Stamina = Mathf.Max(0f, Stamina - sprintDrainPerSec * dt);
                staminaRegenAllowedAt = Runner.SimulationTime + regenDelaySeconds;
                if (Stamina <= 0f) sprintActive = false;
            }
            else
            {
                bool canRegenNow = isGrounded && (Runner.SimulationTime >= staminaRegenAllowedAt);
                if (canRegenNow)
                    Stamina = Mathf.Min(maxStamina, Stamina + regenPerSec * dt);
            }

            // --------------------- Movement ------------------------
            float inputMagnitude = networkInputData.movementInput.magnitude;
            IsLeftGrab  = networkInputData.isLeftGrabPressed;
            IsRightGrab = networkInputData.isRightGrabPressed;

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

                    // Current horizontal velocity and ground target speed
                    Vector3 vel3  = rigidbody3D.linearVelocity;
                    Vector3 horiz = new Vector3(vel3.x, 0f, vel3.z);
                    float   speedNow = maxSpeed * (sprintActive ? sprintMultiplier : 1f);

                    if (isGrounded)
                    {
                        // --- Ground: nudge toward desired run/sprint velocity
                        Vector3 desired = moveDir * speedNow;
                        float maxAccel = 50f;                   // tune if you want snappier ground starts
                        Vector3 deltaV = Vector3.ClampMagnitude(desired - horiz, maxAccel * df);
                        rigidbody3D.AddForce(deltaV, ForceMode.VelocityChange);
                    }
                    else
                    {
                        // --------------------------- Air control ---------------------------
                        Vector3 moveDirSideBias = (moveDir.z * Vector3.forward) + (moveDir.x * airSideBias * Vector3.right);
                        moveDirSideBias.Normalize();

                        float targetBuild = Mathf.Min(speedNow, airMaxSpeed);

                        // Never pick a desired slower than what you had at takeoff
                        float desiredMag  = Mathf.Max(horiz.magnitude, takeoffHorizSpeed, targetBuild);
                        Vector3 desired   = moveDirSideBias * desiredMag;

                        
                        Vector3 add = desired - horiz;

                        // If steering mostly opposite current motion, use softer brake accel
                        float oppose = (horiz.sqrMagnitude > 0.0001f)
                            ? Vector3.Dot(add.normalized, horiz.normalized)
                            : 1f;

                        float perTick = ((oppose < -0.25f) ? airBrake : airAccel) * df;
                        Vector3 deltaV = Vector3.ClampMagnitude(add, perTick);

                        float keepCap = Mathf.Max(airMaxSpeed, takeoffHorizSpeed);
                        if (horiz.magnitude >= keepCap && Vector3.Dot(deltaV, horiz) > 0f)
                        {
                            Vector3 along = Vector3.Project(deltaV, horiz.normalized);
                            deltaV -= along;
                        }

                        rigidbody3D.AddForce(deltaV, ForceMode.VelocityChange);
                    }
                }


               // -------------------------- Jumping ------------------------
                if (networkInputData.isJumpPressed)
                {
                    // Treat as grounded for a short grace after touching ground
                    bool groundedOrGrace = isGrounded || (Runner.SimulationTime - lastGroundedTime) <= coyoteTimeSeconds;

                    // If grounded (or within grace): normal jump, DO NOT detach
                    if (groundedOrGrace)
                    {
                        rigidbody3D.AddForce(Vector3.up * 30f, ForceMode.Impulse);
                        isJumpButtonPressed = false;
                        goto JumpHandled;
                    }

                    // Airborne: only detach+jump if BOTH hands are latched AND BOTH are on kinematic bodies
                    HandGrabHandler leftLatched  = null;
                    HandGrabHandler rightLatched = null;

                    foreach (var h in handGrabHandlers)
                    {
                        if (!h.IsLatched) continue;
                        if (h.Side == HandGrabHandler.HandSide.Left)  leftLatched  = h;
                        if (h.Side == HandGrabHandler.HandSide.Right) rightLatched = h;
                    }

                    if (leftLatched != null && rightLatched != null)
                    {
                        bool bothKinematic = leftLatched.IsLatchedToKinematic && rightLatched.IsLatchedToKinematic;

                        if (bothKinematic)
                        {
                            // Detach both airborne wall-jump
                            leftLatched.ReleaseIfLatched();
                            rightLatched.ReleaseIfLatched();

                            rigidbody3D.AddForce(Vector3.up * 30f, ForceMode.Impulse); // straight up for now
                            isJumpButtonPressed = false;
                            goto JumpHandled;
                        }
                    }
                JumpHandled: ;
                }
            }

            else
            {
                // We are in full ragdoll. Only allow stand-up if NOT dead. CURRENTLY CAN STAND UP WHILE DEAD FOR TESTING
                if (/**!IsDead &&*/ networkInputData.isAwakeButtonPressed && Runner.SimulationTime - lastTimeBecameRagdoll > 3)
                    MakeActiveRagdoll();

            }
        }

        // ----------------- Animation -----------------
        if (Object.HasStateAuthority)
        {
            Vector3 v = rigidbody3D.linearVelocity;
            Vector3 local = transform.InverseTransformDirection(new Vector3(v.x, 0f, v.z));

            float top = Mathf.Max(0.001f, maxSpeed * sprintMultiplier);
            float fwd   = local.z / top;
            float right = local.x / top;

            // normalized movement magnitude for transitions
            float moveMag = Mathf.Clamp01(new Vector2(fwd, right).magnitude);

            // deadzone + damping
            const float dead = 0.05f;
            if (Mathf.Abs(fwd)   < dead) fwd = 0f;
            if (Mathf.Abs(right) < dead) right = 0f;

            animator.SetFloat("Forward",  Mathf.Clamp(fwd,  -1f, 1f),  0.1f, Runner.DeltaTime);
            animator.SetFloat("Right",    Mathf.Clamp(right,-1f, 1f),  0.1f, Runner.DeltaTime);
            animator.SetFloat("MoveMag",  moveMag);

            // playback speed scales with horizontal speed
            float horizSpeed = new Vector2(local.x, local.z).magnitude;
            animator.SetFloat("AnimSpeed", horizSpeed * 0.4f);
            float upStart = -2f;   // start raising just above horizon
            float fullUp  = -10f;  // treat -10° as "fully up" (hits 1.0 early)
            float t = Mathf.InverseLerp(upStart, fullUp, pitchDeg);

            // Ease-out curve so it climbs quickly to near 1.
            float y = Mathf.InverseLerp(maxPitch, minPitch, pitchDeg);

            // Optional shaping so it rises quicker but still continuous (use 1f for linear)
            float shaped = Mathf.Pow(y, 0.7f); // 0.5–0.8 feels good

            // Final raise value: 0.35 at full down, up to 1.0 at full up
            float raiseVal = Mathf.Lerp(0.35f, 1f, shaped);

            // Per-hand gating (only apply when that button is held)
            animator.SetFloat("LeftRaise01",  IsLeftGrab  ? raiseVal : 0f);
            animator.SetFloat("RightRaise01", IsRightGrab ? raiseVal : 0f);


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
        networkInputData.aimYawDeg = visualYawDeg;

        networkInputData.isSprinting = isSprintHeld;

        if (isJumpButtonPressed) networkInputData.isJumpPressed = true;
        if (isAwakeButtonPressed)  networkInputData.isAwakeButtonPressed = true;
        if (isLeftGrabButtonPressed)  networkInputData.isLeftGrabPressed  = true;
        if (isRightGrabButtonPressed) networkInputData.isRightGrabPressed = true;

        // Reset one-shots each tick
        isJumpButtonPressed  = false;
        isAwakeButtonPressed = false;
        isLeftGrabButtonPressed  = false;
        isRightGrabButtonPressed = false;
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
            RpcDeathFade(true);
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
        IsLeftGrab = false;
        IsRightGrab = false;
        CanLook = false;
    }

    void MakeActiveRagdoll()
    {
        if (!Object.HasStateAuthority) return;
        RpcDeathFade(false);
        IsDead = false;
        if (Hp <= 0) Hp = maxHp / 2;

        JointDrive jointDrive = mainJoint.slerpDrive;
        jointDrive.positionSpring = startSlerpPositionSpring;
        mainJoint.slerpDrive = jointDrive;

        for (int i = 0; i < syncPhysicsObjects.Length; i++)
            syncPhysicsObjects[i].MakeActiveRagdoll();

        isActiveRagdoll = true;
        IsLeftGrab = false;
        IsRightGrab = false;
        CanLook = true;
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
        if (!Object.HasStateAuthority && !Object.HasInputAuthority) 
        {
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

        if (Object.HasStateAuthority)
        {
            if (Stamina <= 0f) Stamina = maxStamina;
            if (Hp <= 0) { Hp = maxHp; IsDead = false; }
            yawDeg = transform.eulerAngles.y;
            pitchDeg = Mathf.Clamp(pitchDeg, minPitch, maxPitch);
            if (headJoint) headStartLocalRot = headJoint.transform.localRotation;
            CanLook = true;
        }
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetFloat("_LocalFadeOn", Object.HasInputAuthority ? 1f : 0f);
            r.SetPropertyBlock(mpb);
        }
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (Object.InputAuthority == player)
            Runner.Despawn(Object);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RpcDeathFade(bool fadeIn)
    {
        // This runs on the owner's client
        if (playerFade == null) return;
        if (fadeIn) playerFade.FadeInBlack();
        else        playerFade.FadeOutBlack();
    }
}
