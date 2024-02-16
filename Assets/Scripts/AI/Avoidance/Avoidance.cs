using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using static UnityEngine.GraphicsBuffer;

namespace NavMeshAvoidance
{
    public sealed class Avoidance : MonoBehaviour 
    {
        readonly List<NavMeshAgent> agents = new List<NavMeshAgent>();
        NativeArray<float3> agentsPositions;


        [Tooltip("Agents will ignore others with distance greather than this value. Bigger value can decrease performance.")]
        [SerializeField, Range(0.1f, 100f)] float maxAvoidanceDistance = 3f;
        [Tooltip("Speed of agents \"pushing\" from each other in m/s. Increase to make avoidance more noticeable. Default is 1.")]
        [SerializeField, Range(0f, 300f)] float strength = 1;
        [Tooltip("Agents will try to keep this distance between them. Something like NavMeshAgent.Radius, but same for all agents. Do not make this value bigger than Max Avoidance Distance.")]
        [SerializeField, Range(0.1f, 100f)] float distance = 1;
        [SerializeField] bool showDebugGizmos = true;
        
        public float Distance => distance;

        float sqrMaxAvoidanceDistance;

        void Awake()
        {
            sqrMaxAvoidanceDistance = Mathf.Pow(maxAvoidanceDistance, 2);
        }

        void Start()
        {
            agentsPositions = new NativeArray<float3>(agents.Count, Allocator.Persistent);
        }

        public void Ondestroy()
        {
            agentsPositions.Dispose();

        }

        void Update()
        {
            var deltaTime = Time.deltaTime;

            if (agentsPositions.Length != agents.Count)
            {
                agentsPositions.Dispose();
                agentsPositions = new NativeArray<float3>(agents.Count, Allocator.Persistent);
            }


            for (int i = 0; i < agents.Count; i++)
            {
                // Vector3 is implicitly converted to float3
                agentsPositions[i] = agents[i].transform.position;
            }

            AvoidanceJob job = new AvoidanceJob
            {
                AgentPositions = agentsPositions,
                maxAvoidanceDistance = maxAvoidanceDistance,
                strength = strength,
                AvoidanceVectors = new NativeArray<Vector3>(agents.Count, Allocator.TempJob)
            };

            JobHandle jobHandle = job.Schedule();
            jobHandle.Complete();

            for(int i = 0; i < agents.Count; i++)
            {
                agents[i].Move(job.AvoidanceVectors[i].normalized * distance * deltaTime);
            }
        }

        void CalcualteAvoidance()
        {
            var agentsTotal = agents.Count;
            var deltaTime = Time.deltaTime;
            
            for (var q = 0; q < agentsTotal; q++)
            {
                var agentAPos = agents[q].transform.position;
                
                var avoidanceVector = Vector3.zero;

                for (var w = 0; w < agentsTotal; w++)
                {
                    var agentBPos = agents[w].transform.position;
                    
                    var direction = agentAPos - agentBPos;
                    var sqrDistance = direction.sqrMagnitude;

                    var weakness = sqrDistance / sqrMaxAvoidanceDistance;
                    
                    if (weakness > 1f)
                        continue;
                    
                    direction.y = 0; // i do not sure we need to use Y coord in navmesh directions, so ignoring it
                    
                    avoidanceVector += Vector3.Lerp(direction * strength, Vector3.zero, weakness);
                }
                
                if (showDebugGizmos)
                    Debug.DrawRay(agentAPos, avoidanceVector, Color.green);

                if (agents[q] != null && agents[q].isActiveAndEnabled)
                    agents[q].Move(avoidanceVector.normalized * distance * deltaTime);
            }
        }

        public void AddAgent(NavMeshAgent agent) => agents.Add(agent);
        public void RemoveAgent(NavMeshAgent agent) => agents.Remove(agent);
        
        void OnDrawGizmos()
        {
            if (showDebugGizmos)
                for (var i = 0; i < agents.Count; i++)
                    Gizmos.DrawRay(agents[i].destination, Vector3.up);
        }
    }
}