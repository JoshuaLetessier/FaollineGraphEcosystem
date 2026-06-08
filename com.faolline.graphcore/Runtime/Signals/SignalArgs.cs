using System;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Immutable description of a raised signal: its <see cref="Name"/> and an optional single scalar
    /// payload (<c>bool</c>/<c>int</c>/<c>float</c>/<c>string</c>). <see cref="HasPayload"/> distinguishes a
    /// payload-less signal from one carrying a value. Constructed by <see cref="BaseContext"/> when a signal
    /// is raised; delivered to <c>OnSignal</c> subscribers and stored as the last value for the name.
    /// </summary>
    public readonly struct SignalArgs
    {
        /// <summary>The raised signal's name. Non-empty for any delivered signal.</summary>
        public string Name { get; }

        /// <summary><c>true</c> when a scalar payload accompanied the raise.</summary>
        public bool HasPayload { get; }

        /// <summary>
        /// The boxed scalar payload (<c>bool</c>/<c>int</c>/<c>float</c>/<c>string</c>), or <c>null</c>
        /// when <see cref="HasPayload"/> is <c>false</c>.
        /// </summary>
        public object PayloadBoxed { get; }

        /// <summary>Internal constructor — instances are produced only by <see cref="BaseContext"/>.</summary>
        internal SignalArgs(string name, bool hasPayload, object payloadBoxed)
        {
            Name = name;
            HasPayload = hasPayload;
            PayloadBoxed = payloadBoxed;
        }

        /// <summary>
        /// Returns the payload typed as <typeparamref name="T"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="HasPayload"/> is <c>false</c>.</exception>
        /// <exception cref="InvalidCastException">Thrown when the payload is not of type <typeparamref name="T"/>.</exception>
        public T GetPayload<T>()
        {
            if (!HasPayload)
                throw new InvalidOperationException(
                    $"[GraphCore] Signal '{Name}' has no payload.");
            return (T)PayloadBoxed;
        }
    }
}
