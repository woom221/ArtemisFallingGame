using System;
using System.Collections;
using System.Collections.Generic;
using Analytics;
using UnityEngine;

public class SplashZone : MonoBehaviour
{
    public float timeRemaining = 5;
    public float splashRadius;
    public float explosionDamage;

    public bool damageOverTime;
    public bool explodesOnImpact;

    // Note: These fields only matter if damageOverTime == true.
    public float damageOverTimeDamage = 0.1f;
    // private bool damageOverTimeActive = false;
    public float damageOverTimeCooldown = 0.2f;
    private float damageOverTimeRemaining = 0.2f;

    public ParticleSystem explosion;

    private List<Collider> damageablesInside = new List<Collider>();

    private AnalyticsManager _analytics;

    // Start is called before the first frame update
    void Start()
    {
        transform.localScale = new Vector3(splashRadius, 1, splashRadius);
        explosion.transform.localScale /= splashRadius;
        explosion.transform.parent = null;

        _analytics = FindObjectOfType<AnalyticsManager>();
    }

    // Update is called once per frame
    void Update()
    {   
        if (damageOverTime) {
            if (damageOverTimeRemaining > 0) {
                damageOverTimeRemaining -= Time.deltaTime;
            }
            if (damageOverTimeRemaining <= 0) { // The countdown has expired. Inflict the damage
                foreach (Collider target in damageablesInside) {
                    // TODO: Make it a different timer for each target. Use Coroutines.
                    Controller playerInside = target.gameObject.GetComponent<Controller>();
                    // print($"Splash zone damaging {target}!");
                    playerInside.InflictDamage(damageOverTimeDamage);
                    _analytics.DamageEvent(target.gameObject,gameObject);
                    
                    // damageOverTimeActive = false;
                    damageOverTimeRemaining = damageOverTimeCooldown;
                }
            }// else if (damageOverTimeRemaining == damageOverTimeCooldown) {  // We need to start the countdown.
            //     damageOverTimeActive = true;
            // }
        }

        if (timeRemaining > 0)
        {
            Color objectColour = this.GetComponent<MeshRenderer>().material.color;
            float fadeAmount = objectColour.a + (1 * Time.deltaTime);

            objectColour = new Color(objectColour.r, objectColour.g, objectColour.b, fadeAmount);
            this.GetComponent<MeshRenderer>().material.color = objectColour;
            timeRemaining -= Time.deltaTime;
        }
        else {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter(Collider collider) {
        // print("splash zone trigger was set off");
        if (explodesOnImpact && collider.gameObject.tag == "Player") {
            Controller playerEntered = collider.gameObject.GetComponent<Controller>();

            playerEntered.InflictDamage(explosionDamage);
            _analytics.DamageEvent(collider.gameObject,gameObject);
        }
        if (damageOverTime && collider.gameObject.tag == "Player") {
            print($"Target {collider} entered the zone.");
            if (!damageablesInside.Contains(collider)) {
                damageablesInside.Add(collider);
            }
        }
    }

    void OnTriggerExit(Collider collider) {
        if (damageablesInside.Contains(collider)) {
            damageablesInside.Remove(collider);
        }
    }
}
