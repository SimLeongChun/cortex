namespace Cortex.Streams.Operators.Windows
{
    /// <summary>
    /// Defines how window state is managed when results are emitted.
    /// </summary>
    public enum WindowStateMode
    {
        /// <summary>
        /// Accumulating mode: Window contents continue to accumulate across firings.
        /// Each emission includes all elements since the window started.
        /// Use this when you want cumulative aggregates that update over time.
        /// </summary>
        Accumulating,

        /// <summary>
        /// Discarding mode: Window contents are cleared after each firing.
        /// Each emission only includes elements since the last firing.
        /// Use this when you want incremental/delta results.
        /// </summary>
        Discarding,

        /// <summary>
        /// Accumulating and retracting mode: Like accumulating, but also emits retractions
        /// for previous results when updated results are available.
        /// Use this when downstream operators need to update/replace previous results.
        /// </summary>
        AccumulatingAndRetracting
    }

    /// <summary>
    /// Defines the type of emission from a window.
    /// </summary>
    public enum WindowEmissionType
    {
        /// <summary>
        /// Normal emission - new or updated data.
        /// </summary>
        Normal,

        /// <summary>
        /// Early emission - partial results before window closes.
        /// </summary>
        Early,

        /// <summary>
        /// On-time emission - results at window close time.
        /// </summary>
        OnTime,

        /// <summary>
        /// Late emission - results after window close time (for late-arriving data).
        /// </summary>
        Late,

        /// <summary>
        /// Retraction - indicates previous result should be removed/replaced.
        /// </summary>
        Retraction
    }
}
