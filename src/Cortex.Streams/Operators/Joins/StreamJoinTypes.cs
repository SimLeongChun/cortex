using System;

namespace Cortex.Streams.Operators.Joins
{
    /// <summary>
    /// Represents the result of a stream-stream join operation.
    /// Contains the left element, optional right element, and metadata about the join.
    /// </summary>
    /// <typeparam name="TLeft">The type of the left stream element.</typeparam>
    /// <typeparam name="TRight">The type of the right stream element.</typeparam>
    public class JoinedStreamResult<TLeft, TRight>
    {
        /// <summary>
        /// The element from the left stream.
        /// </summary>
        public TLeft Left { get; }

        /// <summary>
        /// The element from the right stream. May be default if no match was found (in left/outer joins).
        /// </summary>
        public TRight Right { get; }

        /// <summary>
        /// Indicates whether a matching right element was found.
        /// </summary>
        public bool HasRightMatch { get; }

        /// <summary>
        /// Indicates whether a matching left element was found.
        /// For right-side emissions in outer joins, this may be false.
        /// </summary>
        public bool HasLeftMatch { get; }

        /// <summary>
        /// The timestamp when the join was performed.
        /// </summary>
        public DateTime JoinTimestamp { get; }

        /// <summary>
        /// The key that was used for matching.
        /// </summary>
        public object Key { get; }

        /// <summary>
        /// Creates a join result for a matched pair.
        /// </summary>
        public JoinedStreamResult(TLeft left, TRight right, object key, DateTime joinTimestamp)
        {
            Left = left;
            Right = right;
            HasLeftMatch = true;
            HasRightMatch = true;
            Key = key;
            JoinTimestamp = joinTimestamp;
        }

        /// <summary>
        /// Creates a join result with explicit match flags (for left/right/outer joins).
        /// </summary>
        public JoinedStreamResult(TLeft left, TRight right, bool hasLeftMatch, bool hasRightMatch, object key, DateTime joinTimestamp)
        {
            Left = left;
            Right = right;
            HasLeftMatch = hasLeftMatch;
            HasRightMatch = hasRightMatch;
            Key = key;
            JoinTimestamp = joinTimestamp;
        }

        /// <summary>
        /// Creates a left-only result (no matching right element).
        /// </summary>
        public static JoinedStreamResult<TLeft, TRight> LeftOnly(TLeft left, object key, DateTime joinTimestamp)
        {
            return new JoinedStreamResult<TLeft, TRight>(left, default!, true, false, key, joinTimestamp);
        }

        /// <summary>
        /// Creates a right-only result (no matching left element).
        /// </summary>
        public static JoinedStreamResult<TLeft, TRight> RightOnly(TRight right, object key, DateTime joinTimestamp)
        {
            return new JoinedStreamResult<TLeft, TRight>(default!, right, false, true, key, joinTimestamp);
        }
    }

    /// <summary>
    /// Specifies the type of join to perform between two streams.
    /// </summary>
    public enum StreamJoinType
    {
        /// <summary>
        /// Inner join: only emits when both left and right matches are found within the window.
        /// </summary>
        Inner,

        /// <summary>
        /// Left join: emits for every left element. If no right match is found within the window,
        /// emits with default right value when the window expires.
        /// </summary>
        Left,

        /// <summary>
        /// Right join: emits for every right element. If no left match is found within the window,
        /// emits with default left value when the window expires.
        /// </summary>
        Right,

        /// <summary>
        /// Full outer join: emits for every element from both streams.
        /// Unmatched elements are emitted when the window expires.
        /// </summary>
        Outer
    }

    /// <summary>
    /// Configuration options for stream-stream joins.
    /// </summary>
    public class StreamJoinConfiguration
    {
        /// <summary>
        /// The duration of the join window. Events can only be matched within this time window.
        /// </summary>
        public TimeSpan WindowSize { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The type of join to perform.
        /// </summary>
        public StreamJoinType JoinType { get; set; } = StreamJoinType.Inner;

        /// <summary>
        /// How often to check for and clean up expired window data.
        /// </summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Grace period after window expiration before emitting unmatched records (for left/outer joins).
        /// Allows for slightly late-arriving data.
        /// </summary>
        public TimeSpan GracePeriod { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Maximum number of elements to buffer per key on each side.
        /// Prevents memory issues with high-cardinality keys.
        /// </summary>
        public int MaxBufferSizePerKey { get; set; } = 1000;

        /// <summary>
        /// Creates a default configuration with inner join semantics.
        /// </summary>
        public static StreamJoinConfiguration Default => new StreamJoinConfiguration();

        /// <summary>
        /// Creates a configuration for a left join.
        /// </summary>
        public static StreamJoinConfiguration LeftJoin(TimeSpan windowSize) => new StreamJoinConfiguration
        {
            WindowSize = windowSize,
            JoinType = StreamJoinType.Left
        };

        /// <summary>
        /// Creates a configuration for an inner join.
        /// </summary>
        public static StreamJoinConfiguration InnerJoin(TimeSpan windowSize) => new StreamJoinConfiguration
        {
            WindowSize = windowSize,
            JoinType = StreamJoinType.Inner
        };

        /// <summary>
        /// Creates a configuration for a full outer join.
        /// </summary>
        public static StreamJoinConfiguration OuterJoin(TimeSpan windowSize) => new StreamJoinConfiguration
        {
            WindowSize = windowSize,
            JoinType = StreamJoinType.Outer
        };
    }
}
