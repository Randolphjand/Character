﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace WizardsCode.Character
{
    /// <summary>
    /// Make the cahracter wander semi-randomly. They won't necessarily change
    /// direction frequently but will, instead, continue in roughly the same
    /// direction for some time. Eventually they will get bored and change
    /// direction.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Wander : MonoBehaviour
    {
        [SerializeField, Tooltip("The minimum time that a character will continue on a random. If the character reaches a waypoint within this time then they will continue in roughly the same direction.")]
        private float minTimeBetweenRandomPathChanges = 5;
        [SerializeField, Tooltip("The maximum time that a character will continue on a random path.")]
        private float maxTimeBetweenRandomPathChanges = 15;
        [SerializeField, Tooltip("The minimum distance the agent will typically travel on a given path before they change direction.")]
        private float minDistanceOfRandomPathChange = 10;
        [SerializeField, Tooltip("The maximum distance the agent will typically travel on a given path before they change direction.")]
        private float maxDistanceOfRandomPathChange = 20;
        [SerializeField, Tooltip("The minimum angle that the character will deviate from the current path when changing the wander direction.")]
        private float minAngleOfRandomPathChange = -60;
        [SerializeField, Tooltip("The maximum angle that the character will deviate from the current path when changing the wander direction.")]
        private float maxAngleOfRandomPathChange = 60;
        [SerializeField, Tooltip("The approximate maximum range the agent will normally wander from their start position.")]
        private float m_MaxWanderRange = 50f;

        private Vector3 m_TargetPosition;
        private float timeOfNextWanderPathChange;
        private Vector3 m_StartPosition;
        private NavMeshAgent m_Agent;
        private Terrain m_Terrain;

        /// <summary>
        /// Get or set the current target.
        /// </summary>
        virtual public Vector3 currentTarget
        {
            get { return m_TargetPosition; }
            set
            {
                if (m_TargetPosition != value)
                {
                    m_TargetPosition = value;
                    m_Agent.SetDestination(value);
                    timeOfNextWanderPathChange = Random.Range(minTimeBetweenRandomPathChanges, maxTimeBetweenRandomPathChanges);
                }
            }
        }

        internal void Awake()
        {
            m_StartPosition = transform.position;
            m_Agent = GetComponent<NavMeshAgent > ();
            Debug.Assert(m_Agent != null, "Characters with a wander behaviour must also have a NavMesh Agent.");

            Vector3 pos = transform.position;
            m_Terrain = Terrain.activeTerrain;
            if (m_Terrain != null)
            {
                pos.y = m_Terrain.SampleHeight(pos);
            }
            m_Agent.Warp(pos);
        }

        public bool HasReachedTarget
        {
            get
            {
                if (m_Agent.hasPath && !m_Agent.pathPending)
                {
                    if (m_Agent.remainingDistance <= m_Agent.stoppingDistance)
                    {
                        if (!m_Agent.hasPath || m_Agent.velocity.sqrMagnitude == 0f)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        internal void Update()
        {
            if (HasReachedTarget)
            {
                OnReachedTarget();
            }

            if (Time.timeSinceLevelLoad > timeOfNextWanderPathChange || !m_Agent.hasPath || m_Agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                UpdateMove();
                timeOfNextWanderPathChange = Time.timeSinceLevelLoad + Random.Range(minTimeBetweenRandomPathChanges, maxTimeBetweenRandomPathChanges);
            }
        }

        /// <summary>
        /// Called whenever this agent is considering where to move to next.
        /// </summary>
        virtual protected void UpdateMove()
        {   
            UpdateWanderTarget();
        }

        /// <summary>
        /// Update the WanderTarget position.
        /// A new position for the target is chosen within a cone defined by the
        /// minAngleOfRandomPathChange and maxAngleOfRandomPathChange. Optionally,
        /// the cone can extend behind the current agent, which has the effect of 
        /// turning the agent around.
        /// </summary>
        internal void UpdateWanderTarget(int maxAttemptCount = 10)
        {
            bool turning = false;
            int attemptCount = 1;

            while (attemptCount <= maxAttemptCount)
            {
                attemptCount++;
                if (!turning && attemptCount > maxAttemptCount / 2)
                {
                    turning = true;
                }

                Vector3 position;
                float minDistance = minDistanceOfRandomPathChange;
                float maxDistance = maxDistanceOfRandomPathChange;


                float rotation = Random.Range(minAngleOfRandomPathChange, maxAngleOfRandomPathChange);
                Quaternion randAng = Quaternion.Euler(0, rotation, 0);

                if (!turning)
                {
                    position = transform.position + ((randAng * transform.forward) * Random.Range(minDistance, maxDistance));
                }
                else
                {
                    position = transform.position + ((randAng * -transform.forward) * Random.Range(minDistance, maxDistance));
                }

                if (Vector3.Distance(m_StartPosition, position) <= m_MaxWanderRange)
                {
                    if (m_Terrain != null)
                    {
                        position.y = m_Terrain.SampleHeight(position);
                    }

                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(position, out hit, transform.lossyScale.y * 2, NavMesh.AllAreas))
                    {
                        currentTarget = hit.position;
                        return;
                    }
                }
            }

            if (Vector3.Distance(transform.position, m_StartPosition) > minDistanceOfRandomPathChange)
            {
                currentTarget = m_StartPosition;
                return;
            }
            else
            {
                if (m_Terrain != null)
                {
                    float y = m_Terrain.SampleHeight(Vector3.zero);
                    currentTarget = new Vector3(m_Terrain.terrainData.heightmapResolution / 2, y, m_Terrain.terrainData.heightmapResolution / 2);
                    return;
                }
                else
                {
                    currentTarget = Vector3.zero;
                    return;
                }
            }
        }

        /// <summary>
        /// Called when a target has been reached.
        /// </summary>
        internal virtual void OnReachedTarget()
        {
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            DrawWanderAreaGizmo();
            DrawWanderTargetGizmo();
            DrawWanderRangeGizmo();
        }

        protected void DrawWanderRangeGizmo()
        {
            Gizmos.DrawWireSphere(m_StartPosition, m_MaxWanderRange);
        }

        protected void DrawWanderTargetGizmo()
        {
            if (m_TargetPosition != null)
            {
                Gizmos.DrawSphere(currentTarget, 0.5f);
                Gizmos.DrawLine(transform.position, currentTarget);            }
        }

        protected void DrawWanderAreaGizmo()
        {
            float totalWanderArc = Mathf.Abs(minAngleOfRandomPathChange) + Mathf.Abs(maxAngleOfRandomPathChange);
            float rayRange = maxDistanceOfRandomPathChange;
            float halfFOV = totalWanderArc / 2.0f;
            Quaternion leftRayRotation = Quaternion.AngleAxis(-halfFOV, Vector3.up);
            Quaternion rightRayRotation = Quaternion.AngleAxis(halfFOV, Vector3.up);
            Vector3 leftRayDirection = leftRayRotation * transform.forward;
            Vector3 rightRayDirection = rightRayRotation * transform.forward;
            Gizmos.DrawRay(transform.position, leftRayDirection * rayRange);
            Gizmos.DrawRay(transform.position, rightRayDirection * rayRange);
        }
#endif
    }
}