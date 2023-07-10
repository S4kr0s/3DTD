using UnityEngine;
using System.Collections;

namespace PolygonArsenal
{
    public class PolygonProjectileScript : MonoBehaviour
    {
        public GameObject impactParticle;
        public GameObject projectileParticle;
        public GameObject muzzleParticle;
        public GameObject[] trailParticles;
        [Header("Adjust if not using Sphere Collider")]
        public float colliderRadius = 1f;
        [Range(0f, 1f)]
        public float collideOffset = 0.15f;

        // private for storing
        private GameObject _impactParticle;
        private GameObject _projectileParticle;
        private GameObject _muzzleParticle;

        void Start()
        {
            VisualsStart();
        }

        public void VisualsStart()
        {
            if (projectileParticle != null)
            {
                _projectileParticle = Instantiate(projectileParticle, transform.position, transform.rotation) as GameObject;
                _projectileParticle.transform.parent = transform;
            }

            if (muzzleParticle != null)
            {
                _muzzleParticle = Instantiate(muzzleParticle, transform.position, transform.rotation) as GameObject;
                Destroy(_muzzleParticle, 1.5f); // Lifetime of muzzle effect.
            }
        }

        private void OnDisable()
        {
            foreach (TrailRenderer trailRenderer in this.gameObject.GetComponentsInChildren<TrailRenderer>())
            {
                trailRenderer.Clear();
            }
        }

        void FixedUpdate()
        {
            RaycastHit hit;

            float rad;
            if (transform.GetComponent<SphereCollider>())
                rad = transform.GetComponent<SphereCollider>().radius;
            else
                rad = colliderRadius;

            Vector3 dir = transform.GetComponent<Rigidbody>().velocity;
            if (transform.GetComponent<Rigidbody>().useGravity)
                dir += Physics.gravity * Time.deltaTime;
            dir = dir.normalized;

            float dist = transform.GetComponent<Rigidbody>().velocity.magnitude * Time.deltaTime;

            if (Physics.SphereCast(transform.position, rad, dir, out hit, dist))
            {
                transform.position = hit.point + (hit.normal * collideOffset);

                GameObject _impactParticle = Instantiate(impactParticle, transform.position, Quaternion.FromToRotation(Vector3.up, hit.normal)) as GameObject;

                if (hit.transform.tag == "Destructible") // Projectile will destroy objects tagged as Destructible
                {
                    Destroy(hit.transform.gameObject);
                }

                foreach (GameObject trail in trailParticles)
                {
                    GameObject curTrail = transform.Find(_projectileParticle.name + "/" + trail.name).gameObject;
                    curTrail.transform.parent = null;
                    Destroy(curTrail, 3f);
                }
                Destroy(_projectileParticle, 3f);
                Destroy(_impactParticle, 5.0f);
                //Destroy(gameObject);

                ParticleSystem[] trails = GetComponentsInChildren<ParticleSystem>();
                //Component at [0] is that of the parent i.e. this object (if there is any)
                for (int i = 1; i < trails.Length; i++)
                {

                    ParticleSystem trail = trails[i];

                    if (trail.gameObject.name.Contains("Trail"))
                    {
                        trail.transform.SetParent(null);
                        Destroy(trail.gameObject, 2f);
                    }
                }
            }
        }

        public void HasCollided()
        {

            if(impactParticle != null)
                _impactParticle = Instantiate(impactParticle, transform.position, Quaternion.identity) as GameObject;

            foreach (GameObject trail in trailParticles)
            {
                GameObject curTrail = transform.Find(_projectileParticle.name + "/" + trail.name).gameObject;
                curTrail.transform.parent = null;
                Destroy(curTrail, 3f);
            }


            if (projectileParticle != null)
                Destroy(_projectileParticle, 3f);

            if (impactParticle != null)
                Destroy(_impactParticle, 5f);
            //Destroy(gameObject);

            ParticleSystem[] trails = GetComponentsInChildren<ParticleSystem>();
            //Component at [0] is that of the parent i.e. this object (if there is any)
            for (int i = 1; i < trails.Length; i++)
            {

                ParticleSystem trail = trails[i];

                if (trail.gameObject.name.Contains("Trail"))
                {
                    trail.transform.SetParent(null);
                    Destroy(trail.gameObject, 2f);
                }
            }
        }

        public void HasCollidedWithoutDeath()
        {
            _impactParticle = Instantiate(impactParticle, transform.position, Quaternion.identity) as GameObject;
        }
    }
}