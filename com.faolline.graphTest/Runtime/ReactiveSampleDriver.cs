using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphStandard;
using Faolline.GraphLogging;

namespace Faolline.GraphTest
{
    /// <summary>
    /// Sample host that drives a <b>reactive progression</b> graph in Play so the graph editor window shows the
    /// live state map (Locked → Available → Completed) of the run cursor. Every <see cref="_stepInterval"/>
    /// seconds it completes the first currently-Available node, cascading unlocks; when the DAG is fully
    /// completed it pauses and (if <see cref="_loop"/>) replays from scratch — so the map keeps animating for
    /// eyeballing. Drop it on a GameObject, assign the reactive sample graph, and press Play with that graph open.
    /// </summary>
    public sealed class ReactiveSampleDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("The reactive progression graph to evaluate (the one open in the editor window).")]
        private BaseGraph _graph;

        [SerializeField, Tooltip("Context collection key that records completed node ids.")]
        private string _completedSetKey = "completed";

        [SerializeField, Tooltip("Seconds between auto-completing one Available node.")]
        private float _stepInterval = 1.2f;

        [SerializeField, Tooltip("When done, wait, then replay from scratch so the map keeps animating.")]
        private bool _loop = true;

        private ReactiveEvaluator _eval;
        private BaseContext _context;
        private float _timer;
        private bool _finished;

        private void Start() => Build();

        private void Build()
        {
            if (_graph == null)
            {
                Logging.Warning("GraphTest", "[GraphTest] ReactiveSampleDriver: no graph assigned; staying inert.");
                return;
            }
            _context = new BaseContext();
            _eval = new ReactiveEvaluator(_graph, _context, _completedSetKey);
            _eval.Start();   // emits initial states + registers the editor run-cursor probe (Play only)
            _timer = 0f;
            _finished = false;
        }

        private void Update()
        {
            if (_eval == null) return;

            _timer += Time.deltaTime;
            if (_timer < _stepInterval) return;
            _timer = 0f;

            if (_finished)
            {
                if (_loop) Build();   // replay
                return;
            }

            if (!CompleteFirstAvailable())
                _finished = true;     // nothing left to unlock
        }

        // Completes the first node currently in the Available state (deterministic by node order),
        // cascading unlocks. Returns false when no node is Available.
        private bool CompleteFirstAvailable()
        {
            foreach (var node in _graph.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.Id)) continue;
                if (_eval.GetState(node.Id) == ReactiveNodeState.Available)
                {
                    _eval.MarkCompleted(node.Id);
                    return true;
                }
            }
            return false;
        }
    }
}
