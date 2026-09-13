using System;
using UnityEngine;
using Unity.Mathematics;

namespace Voxels.Physics {
    
    /// <summary>
    /// Kinematic character controller that checks collisions between the VoxelBoxCollider attached to the same GameObject and other voxel colliders
    /// </summary>
    [RequireComponent(typeof(VoxelBoxCollider))]
    public class VoxelCharacterController : MonoBehaviour {
        public float stepHeight = 1; // Maximum auto-jump height
        public Vector3 gravity; // Gravitational acceleration
        public int layerMask; // Layers of colliders that are considered

        private new VoxelBoxCollider collider;
        private Vector3 persistentVelocity = Vector3.zero; // Gravity and jump
        public bool isGrounded { get; private set; }

        private string debug;


#if UNITY_EDITOR
        private void Reset() {
            gravity = UnityEngine.Physics.gravity;
            layerMask = 0;
            int layer = gameObject.layer;
            for (int i = 0; i < 32; i++) {
                if (!UnityEngine.Physics.GetIgnoreLayerCollision(layer, i))
                    layerMask |= 1 << i;
            }
        }
#endif

        private void Start() {
            collider = GetComponent<VoxelBoxCollider>();
        }


        /// <summary>
        /// Move the collider and stop if it hits another collider
        /// </summary>
        /// <param name="movement">The movement</param>
        /// <param name="hitInfo">Information about the hit point</param>
        /// <returns>Whether a collider was hit</returns>
        public bool Move(Vector3 movement, out VoxelRaycastHit hitInfo) {
            debug = "";
            bool hit = Move(movement.normalized, movement.magnitude, out hitInfo);
            collider.Reinsert();
            return hit;
        }

        /// <summary>
        /// Move the collider with a velocity and stop if it hits another collider
        /// </summary>
        /// <param name="movement">The velocity</param>
        /// <param name="hitInfo">Information about the hit point</param>
        /// <returns>Whether a collider was hit</returns>
        public bool VelocityMove(Vector3 velocity, out VoxelRaycastHit hitInfo)
            => Move(velocity * Time.deltaTime, out hitInfo);

        /// <summary>
        /// Move the collider without entering other colliders
        /// </summary>
        /// <param name="movement">The movement</param>
        public void Move(Vector3 movement) {
            debug = "";
            for (int i = 0; i < 10; i++) {
            //while (true) {
                Vector3 direction = movement.normalized;
                debug += $"Move1 {i} : {transform.position} {direction} {movement}\n";
                if (Move(direction, movement.magnitude, out VoxelRaycastHit info)) {
                    debug += $"Hit {info.distance} {info.normal} {info.collider}\n";
                    movement -= direction * info.distance;
                    movement -= Vector3.Project(movement, info.normal); // Continue in tangent direction
                }
                else {
                    collider.Reinsert();
                    return;
                }
            }
            throw new Exception("Infinite loop\n" + debug);
        }

        /// <summary>
        /// Move the collider with a velocity without entering other colliders
        /// </summary>
        /// <param name="velocity">The velocity</param>
        public void VelocityMove(Vector3 velocity)
            => Move(velocity * Time.deltaTime);

        /// <summary>
        /// Move the collider with the accumulated gravity and jump velocities without entering other colliders
        /// </summary>
        public void GravityMove() {
            persistentVelocity += gravity * Time.deltaTime;
            if (VelocityMove(persistentVelocity, out VoxelRaycastHit info) && Vector3.Dot(info.normal, gravity) < 0) {
                isGrounded = true;
                ResetGravity();
            }
            else isGrounded = false;
        }

        /// <summary>
        /// Reset the accumulated gravity and jump velocities
        /// </summary>
        public void ResetGravity() => persistentVelocity = Vector3.zero;

        /// <summary>
        /// Start a jump
        /// </summary>
        /// <param name="height">Jump height</param>
        public void Jump(float height) {
            persistentVelocity -= Mathf.Sqrt(2 * gravity.magnitude * height) * gravity.normalized;
        }

        
        /// <summary>
        /// Move the collider and stop if it hits another collider  
        /// </summary>
        /// <param name="direction">Direction of the movement</param>
        /// <param name="distance">Distance of the movement</param>
        /// <param name="hitInfo">Information about the hit point</param>
        /// <returns>Whether a collider was hit</returns>
        private bool Move(Vector3 direction, float distance, out VoxelRaycastHit hitInfo) {
            float remainingDistance = distance;
            for (int i = 0; i < 10; i++) {
            //while (true) {
                Box box = collider.Box;
                debug += $"Move2 {i} : {transform.position} {box} {direction} {remainingDistance}\n";
                if (VoxelPhysics.Instance.MoveBox(box, direction, remainingDistance, layerMask, collider, out VoxelRaycastHit info)) {
                    debug += $"Hit {info.distance} {info.normal} {info.collider}\n";
                    float space = Mathf.Min(remainingDistance, -0.001f / Vector3.Dot(direction, info.normal));
                    transform.position += direction * (info.distance - space);
                    remainingDistance -= info.distance - space;
                    if (remainingDistance > 2 * space && Vector3.Dot(info.normal, gravity) == 0) { // Try auto-jump
                        transform.position += direction * (2 * space);
                        remainingDistance -= 2 * space;
                        if (AutoJump()) continue;
                        transform.position -= direction * (2 * space);
                        remainingDistance += 2 * space;
                    }
                    hitInfo = new VoxelRaycastHit(distance - remainingDistance, info.normal, info.collider);
                    return true;
                }
                else {
                    transform.position += direction * remainingDistance;
                    hitInfo = default;
                    return false;
                }
            }
            throw new Exception("Infinite loop\n" + debug);
        }

        /// <summary>
        /// Try to auto-jump to get out of a collider
        /// </summary>
        /// <returns>Whether the auto-jump was successful</returns>
        private bool AutoJump() {
            // Remove [stepHeight] from the bottom of the collider
            Box box = collider.Box;
            float3 autoJump = -stepHeight * gravity.normalized;
            Box shrunk = new(
                math.max(box.min, box.min + autoJump),
                math.min(box.max, box.max + autoJump)
            );

            // Fall to find height
            VoxelPhysics.Instance.MoveBox(shrunk, gravity.normalized, stepHeight, layerMask, collider, out VoxelRaycastHit info);
            float height = stepHeight - info.distance;

            // Check if the collider fits
            if (info.distance == 0) return false;
            if (VoxelPhysics.Instance.MoveBox(shrunk, -gravity.normalized, height, layerMask, collider, out _)) return false;

            debug += $"AutoJump {shrunk} {info.distance} {info.normal} {info.collider}\n";
            transform.position -= gravity.normalized * (height + 0.001f);
            return true;
        }
    }

}