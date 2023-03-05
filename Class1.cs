using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;

namespace ExplodingFireballs
{
    public class ExplosionModule : ItemModule
    {
        public float explosionRadius = 10;
        public float explosionForce = 25;
        public float explosionDamage = 20;
        public string explosionEffectId = "FireballExplosion";
        public bool explosionEffectAutoScaling = true;
        public Vector3 explosionEffectScale = new Vector3(1, 1, 1);
        public override void OnItemLoaded(Item item)
        {
            base.OnItemLoaded(item);
            item.gameObject.AddComponent<ExplosionComponent>().Setup(explosionRadius, explosionForce, explosionDamage, explosionEffectId, explosionEffectAutoScaling, explosionEffectScale);
        }
    }
    public class BurnModule : ItemModule
    {
        public float burnDamagePerSecond = 5;
        public float burnDuration = 10;
        public string burnEffectId = "ImbueFireRagdoll";
        public override void OnItemLoaded(Item item)
        {
            base.OnItemLoaded(item);
            item.gameObject.AddComponent<BurnComponent>().Setup(burnDamagePerSecond, burnDuration, burnEffectId);
        }
    }
    public class ExplosionComponent : MonoBehaviour
    {
        Item item;
        float explosionRadius;
        float explosionForce;
        float explosionDamage;
        string explosionEffectId;
        bool explosionEffectAutoScaling;
        Vector3 explosionEffectScale;
        public void Start()
        {
            item = GetComponent<Item>();
            item.mainCollisionHandler.OnCollisionStartEvent += MainCollisionHandler_OnCollisionStartEvent;
        }
        public void Setup(float radius, float force, float damage, string effect, bool auto, Vector3 scale)
        {
            explosionRadius = radius;
            explosionForce = force;
            explosionDamage = damage;
            explosionEffectId = effect;
            explosionEffectAutoScaling = auto;
            explosionEffectScale = scale;
        }
        private void MainCollisionHandler_OnCollisionStartEvent(CollisionInstance collisionInstance)
        {
            Impact(collisionInstance.contactPoint, collisionInstance.contactNormal, collisionInstance.sourceColliderGroup.transform.up);
        }
        private void Impact(Vector3 contactPoint, Vector3 contactNormal, Vector3 contactNormalUpward)
        {
            EffectInstance effectInstance = Catalog.GetData<EffectData>(explosionEffectId).Spawn(contactPoint, Quaternion.LookRotation(-contactNormal, contactNormalUpward));
            effectInstance.SetIntensity(1f);
            effectInstance.Play(); 
            foreach (Effect effect in effectInstance.effects)
            {
                effect.transform.localScale = explosionEffectAutoScaling ? Vector3.one * (explosionRadius / 10) : explosionEffectScale;
            }
            Collider[] sphereContacts = Physics.OverlapSphere(contactPoint, explosionRadius, 232799233);
            List<Creature> creaturesPushed = new List<Creature>();
            List<Rigidbody> rigidbodiesPushed = new List<Rigidbody>();
            rigidbodiesPushed.Add(item.rb);
            creaturesPushed.Add(Player.local.creature);
            foreach (Creature creature in Creature.allActive)
            {
                if (!creature.isPlayer && !creature.isKilled && Vector3.Distance(contactPoint, creature.transform.position) < explosionRadius && !creaturesPushed.Contains(creature))
                {
                    CollisionInstance collision = new CollisionInstance(new DamageStruct(DamageType.Energy, explosionDamage));
                    collision.damageStruct.hitRagdollPart = creature.ragdoll.rootPart;
                    creature.Damage(collision);
                    creature.ragdoll.SetState(Ragdoll.State.Destabilized);
                    if (item.GetComponent<BurnComponent>() is BurnComponent burn && creature.GetComponent<Burning>() == null) 
                        creature.gameObject.AddComponent<Burning>().Setup(burn.burnDamagePerSecond, burn.burnDuration, burn.burnEffectId);
                    if (item?.lastHandler?.creature != null)
                    {
                        creature.lastInteractionTime = Time.time;
                        creature.lastInteractionCreature = item.lastHandler.creature;
                    }
                    creaturesPushed.Add(creature);
                }
            }
            foreach (Collider collider in sphereContacts)
            {
                if (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic && Vector3.Distance(contactPoint, collider.transform.position) < explosionRadius)
                {
                    if (collider.attachedRigidbody.gameObject.layer != GameManager.GetLayer(LayerName.NPC) && !rigidbodiesPushed.Contains(collider.attachedRigidbody))
                    {
                        collider.attachedRigidbody.AddExplosionForce(explosionForce, contactPoint, explosionRadius, 0.5f, ForceMode.VelocityChange);
                        rigidbodiesPushed.Add(collider.attachedRigidbody);
                    }
                }
            }
        }
    }
    public class BurnComponent : MonoBehaviour
    {
        Item item;
        public float burnDamagePerSecond;
        public float burnDuration;
        public string burnEffectId;
        public void Start()
        {
            item = GetComponent<Item>();
            item.mainCollisionHandler.OnCollisionStartEvent += MainCollisionHandler_OnCollisionStartEvent;
        }
        public void Setup(float dps, float duration, string effect)
        {
            burnDamagePerSecond = dps;
            burnDuration = duration;
            burnEffectId = effect;
        }

        private void MainCollisionHandler_OnCollisionStartEvent(CollisionInstance collisionInstance)
        {
            if(collisionInstance.targetCollider.GetComponentInParent<Creature>() is Creature creature)
            {
                if (creature.GetComponent<Burning>() != null) Destroy(creature.GetComponent<Burning>());
                creature.gameObject.AddComponent<Burning>().Setup(burnDamagePerSecond, burnDuration, burnEffectId);
            }
        }
    }
    public class Burning : MonoBehaviour
    {
        Creature creature;
        EffectInstance instance;
        float timer;
        float burnDamagePerSecond;
        float burnDuration;
        string burnEffectId;
        public void Start()
        {
            creature = GetComponent<Creature>();
            instance = Catalog.GetData<EffectData>(burnEffectId).Spawn(creature.ragdoll.rootPart.transform, true);
            instance.SetRenderer(creature.GetRendererForVFX(), false);
            instance.SetIntensity(1f);
            instance.Play();
            timer = Time.time;
        }
        public void Setup(float dps, float duration, string effect)
        {
            burnDamagePerSecond = dps;
            burnDuration = duration;
            burnEffectId = effect;
        }
        public void Update()
        {
            if (Time.time - timer >= burnDuration)
            {
                instance.Stop();
                Destroy(this);
            }
            else if (!creature.isKilled)
            {
                CollisionInstance collision = new CollisionInstance(new DamageStruct(DamageType.Energy, burnDamagePerSecond * Time.deltaTime));
                collision.damageStruct.hitRagdollPart = creature.ragdoll.rootPart;
                creature.Damage(collision);
            }
        }
        public void OnDestroy()
        {
            if(instance != null && instance.isPlaying)
            instance.Stop();
        }
    }
}
