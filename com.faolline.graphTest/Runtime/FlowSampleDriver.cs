using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphStandard;
using Faolline.GraphLogging;

namespace Faolline.GraphTest
{
    /// <summary>
    /// Sample host that drives a <b>flow</b> graph in Play so the graph editor window shows the multi-active
    /// fired set of the run cursor. On start it fires the entry node; the synchronous fork/join cascade lights
    /// every fired node (green) with the most-recent fire pulsing. If <see cref="_loop"/>, it resets and re-fires
    /// every <see cref="_refireInterval"/> seconds so the propagation keeps refreshing for eyeballing. Drop it on
    /// a GameObject, assign the flow sample graph, and press Play with that graph open.
    /// </summary>
    public sealed class FlowSampleDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("The flow graph to run (the one open in the editor window).")]
        private BaseGraph _graph;

        [SerializeField, Tooltip("Node id to fire; empty uses the graph's EntryNodeId.")]
        private string _entryNodeId = "";

        [SerializeField, Tooltip("Seconds between reset + re-fire (when looping).")]
        private float _refireInterval = 2.5f;

        [SerializeField, Tooltip("Reset and re-fire on an interval so the cascade keeps animating.")]
        private bool _loop = true;

        private FlowRunner _flow;
        private BaseContext _context;
        private float _timer;

        private void Start()
        {
            if (_graph == null)
            {
                Logging.Warning("GraphTest", "[GraphTest] FlowSampleDriver: no graph assigned; staying inert.");
                return;
            }
            _context = new BaseContext();
            _flow = new FlowRunner(_graph, _context);   // registers the editor run-cursor probe (Play only)
            FireEntry();
        }

        private void Update()
        {
            if (_flow == null || !_loop) return;
            _timer += Time.deltaTime;
            if (_timer < _refireInterval) return;
            _timer = 0f;
            _flow.Reset();   // clear the fired set, then re-fire to re-animate the propagation
            FireEntry();
        }

        private void FireEntry()
        {
            var id = string.IsNullOrEmpty(_entryNodeId) ? _graph.EntryNodeId : _entryNodeId;
            if (!string.IsNullOrEmpty(id)) _flow.Fire(id);
            _timer = 0f;
        }
    }
}
