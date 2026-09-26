using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maze
{
    [DisallowMultipleComponent]
    public sealed class MazeProfilingGroup : MonoBehaviour, IMazeProfilingFeatureProvider
    {
        [SerializeField] private string groupId;
        [SerializeField] private string label;
        [SerializeField] private string category = "Object Group";
        [SerializeField] private bool includeInactiveChildren = true;

        private string ResolvedId => string.IsNullOrWhiteSpace(groupId) ? $"group.{gameObject.scene.name}.{gameObject.name}" : groupId;
        private string ResolvedLabel => string.IsNullOrWhiteSpace(label) ? gameObject.name : label;

        public void AddMazeProfilingFeatures(List<MazeProfilingFeatureHandle> features)
        {
            features.Add(new MazeProfilingFeatureHandle(
                ResolvedId,
                ResolvedLabel,
                category,
                () => gameObject.activeSelf ? "active" : "inactive",
                enabled => gameObject.SetActive(enabled),
                () => CaptureState(),
                RestoreState));
        }

        private GroupState CaptureState()
        {
            var behaviours = GetComponentsInChildren<Behaviour>(includeInactiveChildren);
            var states = new BehaviourState[behaviours.Length];
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                states[i] = new BehaviourState(behaviour, behaviour != null && behaviour.enabled);
            }

            return new GroupState(gameObject.activeSelf, states);
        }

        private void RestoreState(object state)
        {
            if (state is not GroupState groupState)
            {
                return;
            }

            gameObject.SetActive(groupState.activeSelf);
            for (var i = 0; i < groupState.behaviours.Length; i++)
            {
                var behaviourState = groupState.behaviours[i];
                if (behaviourState.behaviour != null)
                {
                    behaviourState.behaviour.enabled = behaviourState.enabled;
                }
            }
        }

        [Serializable]
        private readonly struct GroupState
        {
            public readonly bool activeSelf;
            public readonly BehaviourState[] behaviours;

            public GroupState(bool activeSelf, BehaviourState[] behaviours)
            {
                this.activeSelf = activeSelf;
                this.behaviours = behaviours;
            }
        }

        [Serializable]
        private readonly struct BehaviourState
        {
            public readonly Behaviour behaviour;
            public readonly bool enabled;

            public BehaviourState(Behaviour behaviour, bool enabled)
            {
                this.behaviour = behaviour;
                this.enabled = enabled;
            }
        }
    }
}
