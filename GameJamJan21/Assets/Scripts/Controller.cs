using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;           
using UnityEngine.InputSystem.Controls;
using Object = UnityEngine.Object;


public class Controller : MonoBehaviour
{   
    
    [NonSerialized] public int playerNumber;

    public CharacterController controller;
    public float speed = 6f;
    public float sensitivity = 1;
    public GameObject weaponType;
    private GameObject weapon;
    public float playerHealth { get; private set; } = GlobalStats.baseHealth;

    float turnSmoothVelocity;
    Vector3 moveDirection;
    Vector3 lookDirection;
    new Camera camera;
    bool followingCamera = true;
    CameraSwitch cameraController;
    // public float gravity = 0.000001f; // TODO: OK to delete this?
    public float dashIntensity = 50;
    float currentCooldown;

    public float momentum = 0.85f;
    private float startMomentum;
    public float maxMomentum = 1.5f;
    public GameObject backupCamera;

    private bool currentlyDead;
    // These decrease as time goes on
    private float deathCooldown;
    private float invincibilityCooldown; 

    private StartGame playerController;
    private List<Effect> effects = new List<Effect>();

    // Start is called before the first frame update
    void Start()
    {
        playerController = FindObjectOfType<StartGame>();
        camera = GetComponentInChildren<Camera>();
        cameraController = FindObjectOfType<CameraSwitch>();
        if (camera == null) {
            camera = backupCamera.GetComponentInChildren<Camera>();
            followingCamera = false;
        }
        currentCooldown = 0;
        Debug.Log("Spawning in Weapon");
        weapon = Object.Instantiate(weaponType, gameObject.transform, false);
        Vector3 cur_pos = this.transform.position;
        weapon.transform.position = new Vector3(cur_pos[0] + (this.transform.forward[0] * 0.2f), cur_pos[1], cur_pos[2] + (this.transform.forward[2] * 0.2f));
        weapon.GetComponent<GunController>().setOwner(this);
        startMomentum = momentum;
    }

    public void OnMovement(InputValue value)
    {
        // Read value from control. The type depends on what type of controls.
        // the action is bound to.
        moveDirection = value.Get<Vector3>();
        // IMPORTANT: The given InputValue is only valid for the duration of the callback.
        //            Storing the InputValue references somewhere and calling Get<T>()
        //            later does not work correctly.
    }

    public void OnSwitchCamera() {
        if (cameraController != null) {
            cameraController.SwitchCamera();
        }
    }

    public void OnLook(InputValue value)
    {
        // Read value from control. The type depends on what type of controls.
        // the action is bound to.
        if (value.Get<Vector3>() != lookDirection) {
            // print("LOOK ANGLE: " + value.Get<Vector3>());
        }

        var direction = value.Get<Vector3>();
        if (direction.y != 0)
        {
            Debug.Log(lookDirection);
            var rotation = Quaternion.AngleAxis(direction.y * sensitivity, Vector3.up);
            lookDirection = rotation * transform.rotation * Vector3.forward;
            lookDirection.Normalize();
            Debug.Log(lookDirection);
            Debug.Log(rotation);
        }
        else
            lookDirection = direction * sensitivity;
    }
        // IMPORTANT: The given InputValue is only valid for the duration of the callback.
        //            Storing the InputValue references somewhere and calling Get<T>()
        //            later does not work correctly.
    

    public void OnFire() {
        if (!currentlyDead) {
            print("Fired");
            weapon.GetComponent<GunController>().PrimaryFire();
        }
    }

    public void OnDash() {
        
        if (currentCooldown <= 0) {
            print("Dashed");
            var abs_x = Mathf.Abs(moveDirection.x);
            var abs_z = Mathf.Abs(moveDirection.z);
            if (abs_x == 0 && abs_z == 0) {
                return;
            }
            currentCooldown = GlobalStats.dashCooldown;
            controller.Move(moveDirection * speed * Time.deltaTime * dashIntensity * GetDashBonus());
        } else {
            print("Dash on cooldown!");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (currentlyDead) {
            deathCooldown -= Time.deltaTime;
            if (deathCooldown <= 0) {
                deathCooldown = GlobalStats.deathCooldown;
                playerHealth = GlobalStats.baseHealth;
                currentlyDead = false;
                playerController.RespawnPlayer(transform, playerNumber);
                ResetAttributes();
            }
        }
        if (currentCooldown > 0)
            currentCooldown -= Time.deltaTime;

        if (followingCamera == true)
            camera.transform.localRotation = Quaternion.Euler(lookDirection);

        CharacterController controller = gameObject.GetComponent(typeof(CharacterController)) as CharacterController;
        if (!controller.isGrounded) {
            Vector3 fall = new Vector3(0, -(1), 0);
            controller.Move(fall * Time.deltaTime);
        }
        // if (state != PlayerState.Aiming) {
        
        if (lookDirection.magnitude >= 0.5f) {
            Quaternion newAngle = Quaternion.LookRotation(lookDirection, Vector3.up);
            //print("LOOK VALUE: " + lookDirection + " ADJUSTED ANGLE: " + newAngle);
            this.transform.rotation = newAngle;
            // this.transform.Rotate(lookDirection);
        }
        if (moveDirection.magnitude >= 0.1f) {
            // Handle the actual movement
            moveDirection.y = 0;

            controller.Move((moveDirection).normalized * speed * Time.deltaTime * momentum * GetSpeedBonus());
            if (momentum < maxMomentum)
                momentum += 0.1f * Time.deltaTime;
        } else {
            momentum = startMomentum;
        }
        
        // Tick down all effects
        TickDownEffects();
    }

    void TickDownEffects() {
        foreach(Effect e in new List<Effect>(effects)) {
            e.TickDown();
            if (e.CheckTimer())
                effects.Remove(e);
        }
    }

    float GetSpeedBonus() {
        float totalBonus = 1;
        foreach(Effect e in effects) {
            totalBonus += e.speedBonus;
        }
        return totalBonus;
    }

    float GetDamageBonus() { // Not implemented, will require effects to be added to bullets
        float totalBonus = 1;
        foreach(Effect e in effects) {
            totalBonus += e.damageBonus;
        }
        return totalBonus;
    }

    float GetDashBonus() {
        float totalBonus = 1;
        foreach(Effect e in effects) {
            totalBonus += e.dashBonus;
        }
        return totalBonus;
    }

    public bool InflictDamage(float damageAmount) {
        if (invincibilityCooldown > 0) {
            print("PLAYER TOOK NO DAMAGE.");
            return false;
        }
        print("PLAYER TOOK " + damageAmount + " DAMAGE || HEALTH = " + playerHealth);
        /* TODO: ADD PLAYER HEALTH STUFF */
        // Uncomment/fix the next stuff when health is in
        playerHealth = Mathf.Max(0, playerHealth - damageAmount);
        if (playerHealth <= 0) {
            print("PLAYER DIED");
            PlayerDeath();
        }
        return true;
    }

    private void PlayerDeath() {
        currentlyDead = true;
        // Vector3 newPos = this.transform.position += Vector3.up * 10; // TODO: CHANGE THIS. HOW DO WE "DE-ACTIVATE" THE PLAYER
        print("PLAYER DIED");
        transform.position = transform.position + new Vector3(0, 10, 0);
        // SetActive(false);
    }

    public void ResetAttributes() {
        playerHealth = GlobalStats.baseHealth;
        currentCooldown = GlobalStats.dashCooldown;
    }

    void OnTriggerEnter(Collider collider)
    {
        if (collider.gameObject.tag == "Powerup") {
            PowerupDrop powerup = collider.gameObject.GetComponent<PowerupDrop>();
            effects.Add(powerup.GiveEffect());
            powerup.removePowerup();
            Destroy(collider.gameObject);
        }
    }
}
